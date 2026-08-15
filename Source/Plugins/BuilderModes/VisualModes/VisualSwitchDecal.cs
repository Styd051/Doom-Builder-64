#region ================== Namespaces

using System;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.VisualModes;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
    // styd: reproduces R_RenderSwitch from the original Doom 64 engine (p_switch.c / r_phase3.c)
    // The switch is an independent 32x32 decal, centered on the linedef,
    // and not a texture mapped onto the wall geometry.
    internal sealed class VisualSwitchDecal : BaseVisualGeometrySidedef
    {
        #region ================== Constants

        private const float SWITCH_SIZE = 32f;

        #endregion

        #region ================== Variables

        // styd: along-the-line center/direction/half-width of the decal, used by our
        // PickFastReject override to reject hits that are outside our actual 32-unit
        // horizontal span. Set at the end of a successful Setup(). See PickFastReject
        // for why this is necessary (the base class only checks the Z/height range).
        private Vector2D pickCenter;
        private Vector2D pickDir;
        private float pickHalfWidth;

        #endregion

        #region ================== Constructor / Setup

        public VisualSwitchDecal(BaseVisualMode mode, VisualSector vs, Sidedef s) : base(mode, vs, s)
        {
            this.RenderPass = RenderPass.Mask;
            GC.SuppressFinalize(this);
        }

        // This builds the geometry. Returns false when no geometry created.
        public override bool Setup()
        {
            bool hasback = (Sidedef.Other != null);
            int switchmask = Sidedef.Line.SwitchMask & 0x6000;
            bool switchx08 = (Sidedef.Line.SwitchMask & 0x8000) != 0;
            // styd: read directly from SwitchMask (like the other 3 switch bits) instead
            // of IsFlagSet("65536"), which depended on that flag being registered in
            // linedefflags (Doom64_misc.cfg) - see the comment in
            // LinedefEditForm.SetSwitchMask() for the full story.
            bool checkfloorheight = (Sidedef.Line.SwitchMask & 0x10000) != 0; // ML_CHECKFLOORHEIGHT

            long switchtex = 0;
            bool hasswitchtex = false;
            float switchY = 0f;
            bool foundcase = false;

            if (hasback)
            {
                Sector front = Sidedef.Sector;
                Sector back = Sidedef.Other.Sector;

                // Cas A — upper tier (near ceiling step)
                if (back.CeilHeight < front.CeilHeight)
                {
                    if (switchx08 && !checkfloorheight)
                    {
                        if (switchmask == 0x4000)
                        {
                            switchtex = Sidedef.LongLowTexture;
                            hasswitchtex = (Sidedef.LowTexture.Length > 0) && (Sidedef.LowTexture[0] != '-');
                        }
                        else
                        {
                            switchtex = Sidedef.LongMiddleTexture;
                            hasswitchtex = (Sidedef.MiddleTexture.Length > 0) && (Sidedef.MiddleTexture[0] != '-');
                        }
                        switchY = back.CeilHeight + Sidedef.OffsetY + 48f;
                        foundcase = true;
                    }
                }

                // Cas B — lower tier (near floor step)
                if (!foundcase && front.FloorHeight < back.FloorHeight)
                {
                    if (checkfloorheight && !switchx08)
                    {
                        if (switchmask == 0x2000)
                        {
                            switchtex = Sidedef.LongHighTexture;
                            hasswitchtex = (Sidedef.HighTexture.Length > 0) && (Sidedef.HighTexture[0] != '-');
                        }
                        else
                        {
                            switchtex = Sidedef.LongMiddleTexture;
                            hasswitchtex = (Sidedef.MiddleTexture.Length > 0) && (Sidedef.MiddleTexture[0] != '-');
                        }
                        switchY = back.FloorHeight + Sidedef.OffsetY - 16f;
                        foundcase = true;
                    }
                }

                // Cas C — middle (requires the "Render Mid-Texture" flag = 512 for double-sided)
                if (!foundcase && Sidedef.Line.IsFlagSet("512") && checkfloorheight && switchx08)
                {
                    float mbottom = (front.FloorHeight < back.FloorHeight) ? back.FloorHeight : front.FloorHeight;

                    if (switchmask == 0x2000)
                    {
                        switchtex = Sidedef.LongHighTexture;
                        hasswitchtex = (Sidedef.HighTexture.Length > 0) && (Sidedef.HighTexture[0] != '-');
                    }
                    else
                    {
                        switchtex = Sidedef.LongLowTexture;
                        hasswitchtex = (Sidedef.LowTexture.Length > 0) && (Sidedef.LowTexture[0] != '-');
                    }
                    switchY = mbottom + Sidedef.OffsetY + 48f;
                    foundcase = true;
                }
            }
            else
            {
                // Single-sided : always Cas C
                if (checkfloorheight && switchx08)
                {
                    float mbottom = (float)Sidedef.Sector.FloorHeight;

                    if (switchmask == 0x2000)
                    {
                        switchtex = Sidedef.LongHighTexture;
                        hasswitchtex = (Sidedef.HighTexture.Length > 0) && (Sidedef.HighTexture[0] != '-');
                    }
                    else
                    {
                        switchtex = Sidedef.LongLowTexture;
                        hasswitchtex = (Sidedef.LowTexture.Length > 0) && (Sidedef.LowTexture[0] != '-');
                    }
                    switchY = mbottom + Sidedef.OffsetY + 48f;
                    foundcase = true;
                }
            }

            if (!foundcase || !hasswitchtex)
            {
                base.top = 0;
                base.bottom = 0;
                WorldVertex[] empty = new WorldVertex[0];
                base.SetVertices(empty);
                return false;
            }

            // Load the switch texture
            base.Texture = General.Map.Data.GetTextureImage(switchtex);
            if (base.Texture == null)
            {
                base.Texture = General.Map.Data.MissingTexture3D;
                setuponloadedtexture = switchtex;
            }
            else if (!base.Texture.IsImageLoaded)
            {
                setuponloadedtexture = switchtex;
            }

            float topY = switchY;
            float bottomY = topY - SWITCH_SIZE;

            int color = Sidedef.Sector.ThingColor.GetColor();

            Vector2D v1 = Sidedef.Line.Start.Position;
            Vector2D v2 = Sidedef.Line.End.Position;
            Vector2D center = new Vector2D((v1.x + v2.x) * 0.5f, (v1.y + v2.y) * 0.5f);

            Vector2D dir = (v2 - v1);
            float linelen = dir.GetLength();
            if (linelen < 0.0001f)
            {
                base.top = topY;
                base.bottom = bottomY;
                WorldVertex[] empty = new WorldVertex[0];
                base.SetVertices(empty);
                return false;
            }
            dir /= linelen;

            // styd: slight offset perpendicular to the wall to avoid z-fighting
            // with the middle/upper/lower texture (reproduces the +sin/+cos term from R_RenderSwitch)
            Vector2D normal = new Vector2D(dir.y, -dir.x);
            const float SWITCH_NORMAL_OFFSET = 1.0f;
            if (!Sidedef.IsFront) normal = -normal;

            Vector2D offsetCenter = center + normal * SWITCH_NORMAL_OFFSET;

            float half = SWITCH_SIZE * 0.5f;
            Vector2D p1 = offsetCenter - dir * half;
            Vector2D p2 = offsetCenter + dir * half;

            WorldVertex[] verts = new WorldVertex[6];
            verts[0] = new WorldVertex(p1.x, p1.y, bottomY, color, 0f, 1f);
            verts[1] = new WorldVertex(p1.x, p1.y, topY, color, 0f, 0f);
            verts[2] = new WorldVertex(p2.x, p2.y, topY, color, 1f, 0f);
            verts[3] = verts[0];
            verts[4] = verts[2];
            verts[5] = new WorldVertex(p2.x, p2.y, bottomY, color, 1f, 1f);

            base.top = topY;
            base.bottom = bottomY;
            base.SetVertices(verts);

            // styd: remember the decal's actual horizontal span for PickFastReject -
            // center (unoffset by the z-fighting normal nudge is fine, it's perpendicular
            // to dir so it doesn't change the along-line projection), direction and
            // half-width, matching what was just used to build p1/p2 above.
            pickCenter = center;
            pickDir = dir;
            pickHalfWidth = half;

            return true;
        }

        #endregion

        #region ================== Methods

        // This performs a fast test in object picking
        public override bool PickFastReject(Vector3D from, Vector3D to, Vector3D dir)
        {
            // Same Z/height check as the base class (BaseVisualGeometrySidedef)
            if (!((pickintersect.z >= bottom) && (pickintersect.z <= top)))
                return false;

            // styd: the base implementation stops at the Z check because a normal
            // upper/middle/lower texture always spans the linedef's FULL length
            // horizontally - any point in range on Z is necessarily "inside" it. That
            // assumption doesn't hold for us: we're a fixed 32-unit decal centered on
            // the line, which is often much shorter than the line itself (e.g. a
            // 128-unit line with a 32-unit switch in the middle). Without this check,
            // aiming left or right of the decal - but still within its Z band - would
            // wrongly pick the switch instead of the wall texture beside it.
            Vector2D hit2d = new Vector2D(pickintersect.x, pickintersect.y);
            float along = Vector2D.DotProduct(hit2d - pickCenter, pickDir);
            return Math.Abs(along) <= pickHalfWidth;
        }

        public override string GetTextureName()
        {
            int switchslotmask = Sidedef.Line.SwitchMask & 0x6000;
            if (switchslotmask == 0x2000) return this.Sidedef.HighTexture;
            if (switchslotmask == 0x4000) return this.Sidedef.LowTexture;
            return this.Sidedef.MiddleTexture;
        }

        protected override void SetTexture(string texturename)
        {
            int switchslotmask = Sidedef.Line.SwitchMask & 0x6000;
            if (switchslotmask == 0x2000) this.Sidedef.SetTextureHigh(texturename);
            else if (switchslotmask == 0x4000) this.Sidedef.SetTextureLow(texturename);
            else this.Sidedef.SetTextureMid(texturename);

            General.Map.Data.UpdateUsedTextures();
            this.Setup();
        }

        #endregion
    }
}
