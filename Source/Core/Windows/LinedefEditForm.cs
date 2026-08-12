
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;


#endregion

namespace CodeImp.DoomBuilder.Windows
{
	internal partial class LinedefEditForm : DelayedForm
	{
		// Variables
		private ICollection<Linedef> lines;
		
		// Constructor
		public LinedefEditForm()
		{
			// Initialize
			InitializeComponent();
			
			// Fill flags list
			foreach(KeyValuePair<string, string> lf in General.Map.Config.LinedefFlags)
				flags.Add(lf.Value, lf.Key);

			// Fill actions list
			action.GeneralizedCategories = General.Map.Config.GenActionCategories;
			action.AddInfo(General.Map.Config.SortedLinedefActions.ToArray());

			// Fill activations list
			activation.Items.AddRange(General.Map.Config.LinedefActivates.ToArray());
			foreach(LinedefActivateInfo ai in General.Map.Config.LinedefActivates) udmfactivates.Add(ai.Title, ai);
			
			// Fill universal fields list
			fieldslist.ListFixedFields(General.Map.Config.LinedefFields);
			
			// Initialize image selectors
			fronthigh.Initialize();
			frontmid.Initialize();
			frontlow.Initialize();
			backhigh.Initialize();
			backmid.Initialize();
			backlow.Initialize();

			// Initialize custom fields editor
			fieldslist.Setup("linedef");
			
			// Mixed activations? (UDMF)
			if(General.Map.FormatInterface.HasMixedActivations)
				udmfpanel.Visible = true;
			else if(General.Map.FormatInterface.HasPresetActivations)
				hexenpanel.Visible = true;
			
			// Action arguments?
			if(General.Map.FormatInterface.HasActionArgs)
				argspanel.Visible = true;
			
			// Custom fields?
			if(!General.Map.FormatInterface.HasCustomFields)
				tabs.TabPages.Remove(tabcustom);

            // villsa
            //if (!General.Map.FormatInterface.InDoom64Mode)
                tabs.TabPages.Remove(tabmacros);

			customfrontbutton.Visible = General.Map.FormatInterface.HasCustomFields;
			custombackbutton.Visible = General.Map.FormatInterface.HasCustomFields;
			
			// Arrange panels
			if(General.Map.FormatInterface.HasPresetActivations)
			{
				actiongroup.Height = hexenpanel.Bottom + action.Top + (actiongroup.Width - actiongroup.ClientRectangle.Width);
				this.Height = heightpanel1.Height;
			}
            else if (!General.Map.FormatInterface.HasMixedActivations &&
                    !General.Map.FormatInterface.HasActionArgs &&
                    !General.Map.FormatInterface.HasPresetActivations)
            {
                actiongroup.Height = action.Bottom + action.Top + (actiongroup.Width - actiongroup.ClientRectangle.Width);
                this.Height = heightpanel2.Height;
            }
			
			// Tag?
			if(General.Map.FormatInterface.HasLinedefTag)
			{
				// Match position after the action group
				idgroup.Top = actiongroup.Bottom + actiongroup.Margin.Bottom + idgroup.Margin.Top;
			}
			else
			{
				idgroup.Visible = false;
			}

            // villsa
            if (General.Map.FormatInterface.InDoom64Mode)
            {
                this.activationtype.Show();
                activationtype.Top = idgroup.Bottom + idgroup.Margin.Bottom + activationtype.Margin.Top;
                switchsetupbox.Top = activationtype.Bottom + activationtype.Margin.Bottom + switchsetupbox.Margin.Top;
                this.Height = heightpanel3.Height;
            }
		}

        // villsa 9/12/11 (rewritten styd: corrected against DOOM64-RE - see
        // P_ChangeSwitchTexture in p_switch.c and R_WallPrep in r_phase3.c)
        private void SwitchTextureMask(Linedef l)
        {
            int texture, display;
            GetSwitchChoice(l, out texture, out display);

            if (texture == 1) chkSwitchTextureUpper.Checked = true;       // ML_SWITCHX02 alone ("Top")
            else if (texture == 2) chkSwitchTextureLower.Checked = true;  // ML_SWITCHX04 alone ("Bot")
            else if (texture == 3) chkSwitchTextureMiddle.Checked = true; // both ("Mid")

            if (display == 1) chkSwitchDisplayUpper.Checked = true;       // ML_SWITCHX08 alone
            else if (display == 2) chkSwitchDisplayLower.Checked = true;  // ML_CHECKFLOORHEIGHT alone
            else if (display == 3) chkSwitchDisplayMiddle.Checked = true; // both
        }

        // villsa 9/12/11 (rewritten styd: corrected against DOOM64-RE - see
        // P_ChangeSwitchTexture in p_switch.c and R_WallPrep in r_phase3.c)
        private void SetSwitchMask(Linedef l)
        {
            // styd: this is called unconditionally from apply_Click() regardless of game
            // mode. Switch Setup only exists for Doom 64, so bail out for other formats -
            // otherwise SwitchMask (and the checkbox states left over from a Doom64 map)
            // could get written into non-Doom64 maps (Hexen/UDMF) every time this dialog
            // is used, even though those formats have no use for it.
            if (!General.Map.FormatInterface.InDoom64Mode)
                return;

            // styd: if any of the 6 switch checkboxes is still in its "mixed / untouched"
            // indeterminate state (multi-selection differs and the user didn't touch this
            // group), leave this linedef's SwitchMask exactly as it was instead of
            // clobbering it. This replaces the old ".Enabled == false" guard that used to
            // block Switch Setup entirely for multi-selection.
            if (chkSwitchTextureUpper.CheckState == CheckState.Indeterminate ||
                chkSwitchTextureMiddle.CheckState == CheckState.Indeterminate ||
                chkSwitchTextureLower.CheckState == CheckState.Indeterminate ||
                chkSwitchDisplayUpper.CheckState == CheckState.Indeterminate ||
                chkSwitchDisplayMiddle.CheckState == CheckState.Indeterminate ||
                chkSwitchDisplayLower.CheckState == CheckState.Indeterminate)
                return;

            // styd: texture slot that carries/animates the switch graphic - confirmed
            // against SWITCHMASK()/P_ChangeSwitchTexture in DOOM64-RE (p_switch.c):
            // ML_SWITCHX02 alone = Top, ML_SWITCHX04 alone = Bot, both = Mid.
            int texbits = 0;
            if (chkSwitchTextureUpper.Checked) texbits = 0x2000;
            else if (chkSwitchTextureLower.Checked) texbits = 0x4000;
            else if (chkSwitchTextureMiddle.Checked) texbits = (0x2000 | 0x4000);

            // styd: display position of the static switch decal - confirmed against
            // R_WallPrep's 3 R_RenderSwitch call sites in DOOM64-RE (r_phase3.c), which
            // are gated purely by ML_SWITCHX08 / ML_CHECKFLOORHEIGHT, INDEPENDENTLY of
            // the texture bits above:
            //   SWITCHX08 alone        -> near the ceiling ("Upper")
            //   CHECKFLOORHEIGHT alone -> near the floor ("Lower")
            //   both                   -> near the middle ("Middle")
            //   neither                -> no decal drawn at all
            // The old code never set CHECKFLOORHEIGHT here at all, so "Middle" and
            // "Lower" either showed no switch in-game or silently collapsed to the same
            // ceiling position as "Upper".
            bool dispUpper = chkSwitchDisplayUpper.Checked;
            bool dispLower = chkSwitchDisplayLower.Checked;
            bool dispMiddle = chkSwitchDisplayMiddle.Checked;

            if (texbits != 0 && (dispUpper || dispLower || dispMiddle))
            {
                if (dispUpper || dispMiddle)
                    texbits |= 0x8000; // ML_SWITCHX08

                // styd: ML_CHECKFLOORHEIGHT (0x10000) written directly into SwitchMask,
                // same as the other 3 switch bits, instead of going through the general
                // Flags dictionary (l.SetFlag("65536", ...)). That relied on "65536"
                // being registered as a named flag in linedefflags (Doom64_misc.cfg) -
                // if that entry is missing or out of sync between the source config and
                // the actually-deployed one, ReadLinedefs() never reconstructs the
                // "65536" dictionary entry on load even though the raw bit is correctly
                // present in the WAD, so the Display choice silently reverts to "none"
                // after a save+reload. Storing it in SwitchMask removes that dependency
                // entirely - ReadLinedefs() now captures this bit with a direct test,
                // exactly like it already does for the other 3.
                if (dispLower || dispMiddle)
                    texbits |= 0x10000; // ML_CHECKFLOORHEIGHT

                l.SwitchMask = texbits;
            }
            else
            {
                l.SwitchMask = 0;
            }
        }

        // styd: read-only decode, used both to seed the checkboxes (SwitchTextureMask)
        // and to compare linedefs against each other during multi-selection, without
        // mutating any UI state. texture: 0=none, 1=Upper(Top), 2=Lower(Bot), 3=Middle(Mid).
        // display: 0=none, 1=Upper(SWITCHX08 alone), 2=Lower(CHECKFLOORHEIGHT alone),
        // 3=Middle(both). Corrected against DOOM64-RE: texture from SWITCHMASK()
        // (p_switch.c), display from the independent SWITCHX08/CHECKFLOORHEIGHT pair
        // (r_phase3.c R_WallPrep) - see SetSwitchMask() above for the full reasoning.
        private void GetSwitchChoice(Linedef l, out int texture, out int display)
        {
            texture = 0;
            display = 0;

            int mask = l.SwitchMask & 0x6000;
            if (mask == 0x2000) texture = 1;
            else if (mask == 0x4000) texture = 2;
            else if (mask == 0x6000) texture = 3;

            bool switchx08 = (l.SwitchMask & 0x8000) == 0x8000;
            bool checkfloorheight = (l.SwitchMask & 0x10000) == 0x10000;

            if (switchx08 && checkfloorheight) display = 3;
            else if (switchx08) display = 1;
            else if (checkfloorheight) display = 2;
        }

        // styd: compares one linedef's boolean state for a single Switch Setup checkbox
        // against the checkbox's current (seeded) state, and marks it indeterminate on
        // mismatch - same convention as CheckActivationState() for Activation Type, so
        // only the checkboxes that actually differ across the selection turn gray;
        // the rest keep showing the first linedef's value.
        private void CheckSwitchCheckboxState(CheckBox c, bool linedefValue)
        {
            if (c.CheckState == CheckState.Indeterminate)
                return; // already flagged as mixed by an earlier linedef in this selection

            if (linedefValue != c.Checked)
            {
                c.ThreeState = true;
                c.CheckState = CheckState.Indeterminate;
            }
        }

        private void PreSetActivationFlag(CheckBox c, int flag, int mask)
        {
            if ((flag & mask) == mask)
                c.Checked = true;
        }

        private void CheckActivationState(CheckBox c, int flag, int mask)
        {
            if (((flag & mask) != mask) && c.Checked == true)
            {
                c.CheckState = CheckState.Indeterminate;
                c.ThreeState = true;
            }
        }

        private void SetActivationFlag(Linedef l, CheckBox c, int mask)
        {
            if (c.CheckState == CheckState.Checked)
                l.Activate |= mask;
            else if (c.CheckState == CheckState.Unchecked)
                l.Activate &= ~mask;
        }
		
		// This sets up the form to edit the given lines
		public void Setup(ICollection<Linedef> lines)
		{
			LinedefActivateInfo sai;
			Linedef fl;
			
			// Keep this list
			this.lines = lines;
			if(lines.Count > 1) this.Text = "Edit Linedefs (" + lines.Count + ")";

            ////////////////////////////////////////////////////////////////////////
            // Set all options to the first linedef properties
            ////////////////////////////////////////////////////////////////////////

            // styd: Switch Setup used to be entirely disabled for multi-selection
            // ("20120219 villsa - I am lazy, go away..."). It now stays usable and
            // uses the same indeterminate (mixed) tri-state as the rest of the form -
            // see the "for all lines" loop below and SetSwitchMask().
            chkSwitchTextureLower.Enabled = true;
            chkSwitchTextureMiddle.Enabled = true;
            chkSwitchTextureUpper.Enabled = true;
            chkSwitchDisplayLower.Enabled = true;
            chkSwitchDisplayMiddle.Enabled = true;
            chkSwitchDisplayUpper.Enabled = true;

            // Get first line
            fl = General.GetByIndex(lines, 0);
			
			// Flags
			foreach(CheckBox c in flags.Checkboxes)
				if(fl.Flags.ContainsKey(c.Tag.ToString())) c.Checked = fl.Flags[c.Tag.ToString()];
			
			// Activations
			foreach(LinedefActivateInfo ai in activation.Items)
				if((fl.Activate & ai.Index) == ai.Index) activation.SelectedItem = ai;

			// UDMF Activations
			foreach(CheckBox c in udmfactivates.Checkboxes)
			{
				LinedefActivateInfo ai = (c.Tag as LinedefActivateInfo);
				if(fl.Flags.ContainsKey(ai.Key)) c.Checked = fl.Flags[ai.Key];
			}

            // Action/tags
            action.Value = fl.Action;
            tag.Text = fl.Tag.ToString();
            arg0.SetValue(fl.Args[0]);
            arg1.SetValue(fl.Args[1]);
            arg2.SetValue(fl.Args[2]);
            arg3.SetValue(fl.Args[3]);
            arg4.SetValue(fl.Args[4]);

            // styd: seed the Switch Setup checkboxes from the first selected linedef -
            // the "for all lines" loop below then compares each checkbox independently
            // (CheckSwitchCheckboxState) against this seeded state, same convention as
            // Activation Type.
            if (General.Map.FormatInterface.InDoom64Mode)
            {
                SwitchTextureMask(fl);
            }

            // Front side and back side checkboxes
            frontside.Checked = (fl.Front != null);
			backside.Checked = (fl.Back != null);

			// Front settings
			if(fl.Front != null)
			{
				fronthigh.TextureName = fl.Front.HighTexture;
				frontmid.TextureName = fl.Front.MiddleTexture;
				frontlow.TextureName = fl.Front.LowTexture;
				fronthigh.Required = fl.Front.HighRequired();
				frontmid.Required = fl.Front.MiddleRequired();
				frontlow.Required = fl.Front.LowRequired();
				frontsector.Text = fl.Front.Sector.Index.ToString();
				frontoffsetx.Text = fl.Front.OffsetX.ToString();
				frontoffsety.Text = fl.Front.OffsetY.ToString();
			}

			// Back settings
			if(fl.Back != null)
			{
				backhigh.TextureName = fl.Back.HighTexture;
				backmid.TextureName = fl.Back.MiddleTexture;
				backlow.TextureName = fl.Back.LowTexture;
				backhigh.Required = fl.Back.HighRequired();
				backmid.Required = fl.Back.MiddleRequired();
				backlow.Required = fl.Back.LowRequired();
				backsector.Text = fl.Back.Sector.Index.ToString();
				backoffsetx.Text = fl.Back.OffsetX.ToString();
				backoffsety.Text = fl.Back.OffsetY.ToString();
			}

			// Custom fields
			fieldslist.SetValues(fl.Fields, true);

			////////////////////////////////////////////////////////////////////////
			// Now go for all lines and change the options when a setting is different
			////////////////////////////////////////////////////////////////////////

			// Go for all lines
			foreach(Linedef l in lines)
			{
				// Flags
				foreach(CheckBox c in flags.Checkboxes)
				{
					if(l.Flags.ContainsKey(c.Tag.ToString()))
					{
						if(l.Flags[c.Tag.ToString()] != c.Checked)
						{
							c.ThreeState = true;
							c.CheckState = CheckState.Indeterminate;
						}
					}
				}

				// Activations

                // villsa
                if (General.Map.FormatInterface.InDoom64Mode)
                {
                    if (l.Activate > 0)
                    {
                        // 20120219 villsa
                        l.Activate -= (l.Activate & 511);
                        PreSetActivationFlag(activationtypered, l.Activate, 512);
                        PreSetActivationFlag(activationtypeblue, l.Activate, 1024);
                        PreSetActivationFlag(activationtypeyellow, l.Activate, 2048);
                        PreSetActivationFlag(activationtypecross, l.Activate, 4096);
                        PreSetActivationFlag(activationtypeshoot, l.Activate, 8192);
                        PreSetActivationFlag(activationtypeuse, l.Activate, 16384);
                        PreSetActivationFlag(activationtyperepeat, l.Activate, 32768);
                    }

                    // styd: multi-selection support - compare each Switch Setup checkbox
                    // independently against this linedef, same convention as Activation
                    // Type (CheckActivationState) and the general Flags list: only the
                    // checkboxes that actually differ across the selection turn
                    // indeterminate (gray), the rest keep showing the first linedef's
                    // value in plain white/unchecked.
                    int ltexture, ldisplay;
                    GetSwitchChoice(l, out ltexture, out ldisplay);
                    CheckSwitchCheckboxState(chkSwitchTextureUpper, ltexture == 1);
                    CheckSwitchCheckboxState(chkSwitchTextureLower, ltexture == 2);
                    CheckSwitchCheckboxState(chkSwitchTextureMiddle, ltexture == 3);
                    CheckSwitchCheckboxState(chkSwitchDisplayUpper, ldisplay == 1);
                    CheckSwitchCheckboxState(chkSwitchDisplayLower, ldisplay == 2);
                    CheckSwitchCheckboxState(chkSwitchDisplayMiddle, ldisplay == 3);
                }
                else
                {
                    if (activation.Items.Count > 0)
                    {
                        sai = (activation.Items[0] as LinedefActivateInfo);
                        foreach (LinedefActivateInfo ai in activation.Items)
                            if ((l.Activate & ai.Index) == ai.Index) sai = ai;
                        if (sai != activation.SelectedItem) activation.SelectedIndex = -1;
                    }
                }

				// UDMF Activations
				foreach(CheckBox c in udmfactivates.Checkboxes)
				{
					LinedefActivateInfo ai = (c.Tag as LinedefActivateInfo);
					if(l.Flags.ContainsKey(ai.Key))
					{
						if(c.Checked != l.Flags[ai.Key])
						{
							c.ThreeState = true;
							c.CheckState = CheckState.Indeterminate;
						}
					}
				}

				// Action/tags
				if(l.Action != action.Value) action.Empty = true;
				if(l.Tag.ToString() != tag.Text) tag.Text = "";
				if(l.Args[0] != arg0.GetResult(-1)) arg0.ClearValue();
				if(l.Args[1] != arg1.GetResult(-1)) arg1.ClearValue();
				if(l.Args[2] != arg2.GetResult(-1)) arg2.ClearValue();
				if(l.Args[3] != arg3.GetResult(-1)) arg3.ClearValue();
				if(l.Args[4] != arg4.GetResult(-1)) arg4.ClearValue();
				
				// Front side checkbox
				if((l.Front != null) != frontside.Checked)
				{
					frontside.ThreeState = true;
					frontside.CheckState = CheckState.Indeterminate;
					frontside.AutoCheck = false;
				}

				// Back side checkbox
				if((l.Back != null) != backside.Checked)
				{
					backside.ThreeState = true;
					backside.CheckState = CheckState.Indeterminate;
					backside.AutoCheck = false;
				}

				// Front settings
				if(l.Front != null)
				{
					if(fronthigh.TextureName != l.Front.HighTexture) fronthigh.TextureName = "";
					if(frontmid.TextureName != l.Front.MiddleTexture) frontmid.TextureName = "";
					if(frontlow.TextureName != l.Front.LowTexture) frontlow.TextureName = "";
					if(fronthigh.Required != l.Front.HighRequired()) fronthigh.Required = false;
					if(frontmid.Required != l.Front.MiddleRequired()) frontmid.Required = false;
					if(frontlow.Required != l.Front.LowRequired()) frontlow.Required = false;
					if(frontsector.Text != l.Front.Sector.Index.ToString()) frontsector.Text = "";
					if(frontoffsetx.Text != l.Front.OffsetX.ToString()) frontoffsetx.Text = "";
					if(frontoffsety.Text != l.Front.OffsetY.ToString()) frontoffsety.Text = "";
				}

				// Back settings
				if(l.Back != null)
				{
					if(backhigh.TextureName != l.Back.HighTexture) backhigh.TextureName = "";
					if(backmid.TextureName != l.Back.MiddleTexture) backmid.TextureName = "";
					if(backlow.TextureName != l.Back.LowTexture) backlow.TextureName = "";
					if(backhigh.Required != l.Back.HighRequired()) backhigh.Required = false;
					if(backmid.Required != l.Back.MiddleRequired()) backmid.Required = false;
					if(backlow.Required != l.Back.LowRequired()) backlow.Required = false;
					if(backsector.Text != l.Back.Sector.Index.ToString()) backsector.Text = "";
					if(backoffsetx.Text != l.Back.OffsetX.ToString()) backoffsetx.Text = "";
					if(backoffsety.Text != l.Back.OffsetY.ToString()) backoffsety.Text = "";
					if(General.Map.FormatInterface.HasCustomFields) custombackbutton.Visible = true;
				}
				
				// Custom fields
				fieldslist.SetValues(l.Fields, false);
			}

            foreach (Linedef l in lines)
            {
                // 20120219 villsa
                l.Activate -= (l.Activate & 511);
                CheckActivationState(activationtypered, l.Activate, 512);
                CheckActivationState(activationtypeblue, l.Activate, 1024);
                CheckActivationState(activationtypeyellow, l.Activate, 2048);
                CheckActivationState(activationtypecross, l.Activate, 4096);
                CheckActivationState(activationtypeshoot, l.Activate, 8192);
                CheckActivationState(activationtypeuse, l.Activate, 16384);
                CheckActivationState(activationtyperepeat, l.Activate, 32768);
            }
			
			// Refresh controls so that they show their image
			backhigh.Refresh();
			backmid.Refresh();
			backlow.Refresh();
			fronthigh.Refresh();
			frontmid.Refresh();
			frontlow.Refresh();
		}
		
		// Front side (un)checked
		private void frontside_CheckStateChanged(object sender, EventArgs e)
		{
			// Enable/disable panel
			// NOTE: Also enabled when checkbox is grayed!
			frontgroup.Enabled = (frontside.CheckState != CheckState.Unchecked);
		}

		// Back side (un)checked
		private void backside_CheckStateChanged(object sender, EventArgs e)
		{
			// Enable/disable panel
			// NOTE: Also enabled when checkbox is grayed!
			backgroup.Enabled = (backside.CheckState != CheckState.Unchecked);
		}

		// This selects all text in a textbox
		private void SelectAllText(object sender, EventArgs e)
		{
			(sender as TextBox).SelectAll();
		}

		// Apply clicked
		private void apply_Click(object sender, EventArgs e)
		{
			string undodesc = "linedef";
			Sector s;
			int index;
            int activationflag; // villsa
			
			// Verify the tag
			if(General.Map.FormatInterface.HasLinedefTag && ((tag.GetResult(0) < General.Map.FormatInterface.MinTag) || (tag.GetResult(0) > General.Map.FormatInterface.MaxTag)))
			{
				General.ShowWarningMessage("Linedef tag must be between " + General.Map.FormatInterface.MinTag + " and " + General.Map.FormatInterface.MaxTag + ".", MessageBoxButtons.OK);
				return;
			}
			
			// Verify the action
			if((action.Value < General.Map.FormatInterface.MinAction) || (action.Value > General.Map.FormatInterface.MaxAction))
			{
				General.ShowWarningMessage("Linedef action must be between " + General.Map.FormatInterface.MinAction + " and " + General.Map.FormatInterface.MaxAction + ".", MessageBoxButtons.OK);
				return;
			}
			
			// Verify texture offsets
			if((backoffsetx.GetResult(0) < General.Map.FormatInterface.MinTextureOffset) || (backoffsetx.GetResult(0) > General.Map.FormatInterface.MaxTextureOffset) ||
			   (backoffsety.GetResult(0) < General.Map.FormatInterface.MinTextureOffset) || (backoffsety.GetResult(0) > General.Map.FormatInterface.MaxTextureOffset) ||
			   (frontoffsetx.GetResult(0) < General.Map.FormatInterface.MinTextureOffset) || (frontoffsetx.GetResult(0) > General.Map.FormatInterface.MaxTextureOffset) ||
			   (frontoffsety.GetResult(0) < General.Map.FormatInterface.MinTextureOffset) || (frontoffsety.GetResult(0) > General.Map.FormatInterface.MaxTextureOffset))
			{
				General.ShowWarningMessage("Texture offset must be between " + General.Map.FormatInterface.MinTextureOffset + " and " + General.Map.FormatInterface.MaxTextureOffset + ".", MessageBoxButtons.OK);
				return;
			}
			
			// Make undo
			if(lines.Count > 1) undodesc = lines.Count + " linedefs";
			General.Map.UndoRedo.CreateUndo("Edit " + undodesc);
			
			// Go for all the lines
			foreach(Linedef l in lines)
			{
				// Apply all flags
				foreach(CheckBox c in flags.Checkboxes)
				{
					if(c.CheckState == CheckState.Checked) l.SetFlag(c.Tag.ToString(), true);
					else if(c.CheckState == CheckState.Unchecked) l.SetFlag(c.Tag.ToString(), false);
				}
				
				// Apply chosen activation flag
				if(activation.SelectedIndex > -1)
					l.Activate = (activation.SelectedItem as LinedefActivateInfo).Index;

                l.Activate -= (l.Activate & 511);
                SetActivationFlag(l, activationtypered, 512);
                SetActivationFlag(l, activationtypeblue, 1024);
                SetActivationFlag(l, activationtypeyellow, 2048);
                SetActivationFlag(l, activationtypecross, 4096);
                SetActivationFlag(l, activationtypeshoot, 8192);
                SetActivationFlag(l, activationtypeuse, 16384);
                SetActivationFlag(l, activationtyperepeat, 32768);

                SetSwitchMask(l);
				
				// UDMF activations
				foreach(CheckBox c in udmfactivates.Checkboxes)
				{
					LinedefActivateInfo ai = (c.Tag as LinedefActivateInfo);
					if(c.CheckState == CheckState.Checked) l.SetFlag(ai.Key, true);
					else if(c.CheckState == CheckState.Unchecked) l.SetFlag(ai.Key, false);
				}
				
				// Action/tags
				l.Tag = General.Clamp(tag.GetResult(l.Tag), General.Map.FormatInterface.MinTag, General.Map.FormatInterface.MaxTag);
				if(!action.Empty) l.Action = action.Value;
				l.Args[0] = arg0.GetResult(l.Args[0]);
				l.Args[1] = arg1.GetResult(l.Args[1]);
				l.Args[2] = arg2.GetResult(l.Args[2]);
				l.Args[3] = arg3.GetResult(l.Args[3]);
				l.Args[4] = arg4.GetResult(l.Args[4]);
				
				// Remove front side?
				if((l.Front != null) && (frontside.CheckState == CheckState.Unchecked))
				{
					l.Front.Dispose();
				}
				// Create or modify front side?
				else if(frontside.CheckState == CheckState.Checked)
				{
					// Make sure we have a valid sector (make a new one if needed)
					if(l.Front != null) index = l.Front.Sector.Index; else index = -1;
					index = frontsector.GetResult(index);
					if((index > -1) && (index < General.Map.Map.Sectors.Count))
					{
						s = General.Map.Map.GetSectorByIndex(index);
						if(s == null) s = General.Map.Map.CreateSector();
						if(s != null)
						{
							// Create new sidedef?
							if(l.Front == null) General.Map.Map.CreateSidedef(l, true, s);
							if(l.Front != null)
							{
								// Change sector?
								if(l.Front.Sector != s) l.Front.SetSector(s);

								// Apply settings
								l.Front.OffsetX = General.Clamp(frontoffsetx.GetResult(l.Front.OffsetX), General.Map.FormatInterface.MinTextureOffset, General.Map.FormatInterface.MaxTextureOffset);
								l.Front.OffsetY = General.Clamp(frontoffsety.GetResult(l.Front.OffsetY), General.Map.FormatInterface.MinTextureOffset, General.Map.FormatInterface.MaxTextureOffset);
								l.Front.SetTextureHigh(fronthigh.GetResult(l.Front.HighTexture));
								l.Front.SetTextureMid(frontmid.GetResult(l.Front.MiddleTexture));
								l.Front.SetTextureLow(frontlow.GetResult(l.Front.LowTexture));
							}
						}
					}
				}

				// Remove back side?
				if((l.Back != null) && (backside.CheckState == CheckState.Unchecked))
				{
					l.Back.Dispose();
				}
				// Create or modify back side?
				else if(backside.CheckState == CheckState.Checked)
				{
					// Make sure we have a valid sector (make a new one if needed)
					if(l.Back != null) index = l.Back.Sector.Index; else index = -1;
					index = backsector.GetResult(index);
					if((index > -1) && (index < General.Map.Map.Sectors.Count))
					{
						s = General.Map.Map.GetSectorByIndex(index);
						if(s == null) s = General.Map.Map.CreateSector();
						if(s != null)
						{
							// Create new sidedef?
							if(l.Back == null) General.Map.Map.CreateSidedef(l, false, s);
							if(l.Back != null)
							{
								// Change sector?
								if(l.Back.Sector != s) l.Back.SetSector(s);

								// Apply settings
								l.Back.OffsetX = General.Clamp(backoffsetx.GetResult(l.Back.OffsetX), General.Map.FormatInterface.MinTextureOffset, General.Map.FormatInterface.MaxTextureOffset);
								l.Back.OffsetY = General.Clamp(backoffsety.GetResult(l.Back.OffsetY), General.Map.FormatInterface.MinTextureOffset, General.Map.FormatInterface.MaxTextureOffset);
								l.Back.SetTextureHigh(backhigh.GetResult(l.Back.HighTexture));
								l.Back.SetTextureMid(backmid.GetResult(l.Back.MiddleTexture));
								l.Back.SetTextureLow(backlow.GetResult(l.Back.LowTexture));
							}
						}
					}
				}

				// Custom fields
				fieldslist.Apply(l.Fields);
			}

            // villsa
            if (General.Map.FormatInterface.InDoom64Mode)
            {
                // 20120219 villsa - very ugly hack but it'll do for now...
                /*if (action.Value >= 256 && tabs.SelectedTab.Text == "Macros")
                {
                    int id = action.Value - 256;

                    General.Map.Map.Macros[id].SetDataFromTreeNode(mtree);
                }*/
            }

			// Update the used textures
			General.Map.Data.UpdateUsedTextures();
			
			// Done
			General.Map.IsChanged = true;
			this.DialogResult = DialogResult.OK;
			this.Close();
		}

		// Cancel clicked
		private void cancel_Click(object sender, EventArgs e)
		{
			// Be gone
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}

		// This finds a new (unused) tag
		private void newtag_Click(object sender, EventArgs e)
		{
			tag.Text = General.Map.Map.GetNewTag().ToString();
		}

		// Action changes
		private void action_ValueChanges(object sender, EventArgs e)
		{
			int showaction = 0;
			
			// Only when line type is known
			if(General.Map.Config.LinedefActions.ContainsKey(action.Value)) showaction = action.Value;
			
			// Change the argument descriptions
			arg0label.Text = General.Map.Config.LinedefActions[showaction].Args[0].Title + ":";
			arg1label.Text = General.Map.Config.LinedefActions[showaction].Args[1].Title + ":";
			arg2label.Text = General.Map.Config.LinedefActions[showaction].Args[2].Title + ":";
			arg3label.Text = General.Map.Config.LinedefActions[showaction].Args[3].Title + ":";
			arg4label.Text = General.Map.Config.LinedefActions[showaction].Args[4].Title + ":";
			arg0label.Enabled = General.Map.Config.LinedefActions[showaction].Args[0].Used;
			arg1label.Enabled = General.Map.Config.LinedefActions[showaction].Args[1].Used;
			arg2label.Enabled = General.Map.Config.LinedefActions[showaction].Args[2].Used;
			arg3label.Enabled = General.Map.Config.LinedefActions[showaction].Args[3].Used;
			arg4label.Enabled = General.Map.Config.LinedefActions[showaction].Args[4].Used;
			if(arg0label.Enabled) arg0.ForeColor = SystemColors.WindowText; else arg0.ForeColor = SystemColors.GrayText;
			if(arg1label.Enabled) arg1.ForeColor = SystemColors.WindowText; else arg1.ForeColor = SystemColors.GrayText;
			if(arg2label.Enabled) arg2.ForeColor = SystemColors.WindowText; else arg2.ForeColor = SystemColors.GrayText;
			if(arg3label.Enabled) arg3.ForeColor = SystemColors.WindowText; else arg3.ForeColor = SystemColors.GrayText;
			if(arg4label.Enabled) arg4.ForeColor = SystemColors.WindowText; else arg4.ForeColor = SystemColors.GrayText;
			arg0.Setup(General.Map.Config.LinedefActions[showaction].Args[0]);
			arg1.Setup(General.Map.Config.LinedefActions[showaction].Args[1]);
			arg2.Setup(General.Map.Config.LinedefActions[showaction].Args[2]);
			arg3.Setup(General.Map.Config.LinedefActions[showaction].Args[3]);
			arg4.Setup(General.Map.Config.LinedefActions[showaction].Args[4]);
		}

		// Browse Action clicked
		private void browseaction_Click(object sender, EventArgs e)
		{
			action.Value = ActionBrowserForm.BrowseAction(this, action.Value);
		}

		// Custom fields on front sides
		private void customfrontbutton_Click(object sender, EventArgs e)
		{
			// Make collection of front sides
			List<MapElement> sides = new List<MapElement>(lines.Count);
			foreach(Linedef l in lines) if(l.Front != null) sides.Add(l.Front);
			
			// Edit these
			CustomFieldsForm.ShowDialog(this, "Front side custom fields", "sidedef", sides, General.Map.Config.SidedefFields);
		}

		// Custom fields on back sides
		private void custombackbutton_Click(object sender, EventArgs e)
		{
			// Make collection of back sides
			List<MapElement> sides = new List<MapElement>(lines.Count);
			foreach(Linedef l in lines) if(l.Back != null) sides.Add(l.Back);

			// Edit these
			CustomFieldsForm.ShowDialog(this, "Back side custom fields", "sidedef", sides, General.Map.Config.SidedefFields);
		}

		// Help!
		private void LinedefEditForm_HelpRequested(object sender, HelpEventArgs hlpevent)
		{
			General.ShowHelp("w_linedefedit.html");
			hlpevent.Handled = true;
		}

        private void mtagbutton_Click(object sender, EventArgs e)
        {
            mtag.Text = General.Map.Map.GetNewTag().ToString();
        }

        private void mapplytag_Click(object sender, EventArgs e)
        {
            if (mtree.SelectedNode == null)
                return;

            if (mtree.SelectedNode.Parent == null)
                return;

            TreeNode node;
            MacroData data;

            node = mtree.SelectedNode;
            data = (MacroData)node.Tag;

            data.tag = mtag.GetResult(0);

            node.Text = data.SetName();
            node.Tag = data;

            mtag.Text = "";
        }

        private void mbatch_Click(object sender, EventArgs e)
        {
            int batchid = 10;

            mtree.Nodes.Add("Batch");
            mtree.Focus();

            foreach (TreeNode n in mtree.Nodes)
            {
                n.Text = "Batch " + batchid;
                batchid += 10;
            }
        }

        private void maction_Click(object sender, EventArgs e)
        {
            TreeNode node;
            MacroData data;

            if (mtree.SelectedNode == null)
                return;

            if (mtree.SelectedNode.Parent != null)
                return;

            node = mtree.SelectedNode.Nodes.Add("Action");
            mtree.ExpandAll();
            mtree.Focus();

            data = new MacroData();
            data.batch = 0;
            data.type = ActionBrowserForm.BrowseAction(this, action.Value);
            data.tag = 0;

            node.Text = data.SetName();
            node.Tag = data;
        }

        private void tabs_Selected(Object sender, TabControlEventArgs e)
        {
            if (!General.Map.FormatInterface.InDoom64Mode)
                return;

            /*if (e.TabPage.Text != "Macros")  // eek! hack
            {
                if (action.Value >= 256)
                {
                    int id = action.Value - 256;

                    if (General.Map.Map.Macros[id] != null)
                        General.Map.Map.Macros[id].SetDataFromTreeNode(mtree);
                }

                return;
            }*/

            // fill in macros
            mtree.Nodes.Clear();

            /*if (action.Value >= 256)
            {
                int id = action.Value - 256;

                if (General.Map.Map.Macros[id] == null)
                    General.Map.Map.Macros[id] = new Macro(0);

                General.Map.Map.Macros[id].SetNodeFromData(id, mtree);

                mbatch.Enabled = true;
                maction.Enabled = true;
                mdelete.Enabled = true;
                mtagbutton.Enabled = true;
                mapplytag.Enabled = true;
                mtag.Enabled = true;
            }
            else
            {
                mbatch.Enabled = false;
                maction.Enabled = false;
                mdelete.Enabled = false;
                mtagbutton.Enabled = false;
                mapplytag.Enabled = false;
                mtag.Enabled = false;
            }*/
        }

        private void mdelete_Click(object sender, EventArgs e)
        {
            TreeNode n = mtree.SelectedNode;
            bool parentremoved = false;
            int batchid = 10;

            if (n == null)
                return;

            if (n.Parent != null)
            {
                if (n.Parent.Nodes.Count <= 1)
                {
                    n.Parent.Remove();
                    parentremoved = true;
                }
                else
                    n.Remove();
            }
            else
            {
                n.Remove();
                parentremoved = true;
            }

            if (parentremoved == true)
            {
                foreach (TreeNode nn in mtree.Nodes)
                {
                    nn.Text = "Batch " + batchid;
                    batchid += 10;
                }
            }
        }

        private void chkSwitchDisplayUpper_CheckedChanged_1(object sender, EventArgs e)
        {
            // styd: .Checked is ALSO true for CheckState.Indeterminate in WinForms,
            // which made this cascade fire and wipe out sibling checkboxes' mixed
            // state during multi-selection. Only cascade when definitely checked.
            if (this.chkSwitchDisplayUpper.CheckState == CheckState.Checked)
            {
                this.chkSwitchDisplayLower.Checked = false;
                this.chkSwitchDisplayMiddle.Checked = false;
            }
        }

        private void chkSwitchDisplayMiddle_CheckedChanged_1(object sender, EventArgs e)
        {
            // styd: .Checked is ALSO true for CheckState.Indeterminate in WinForms,
            // which made this cascade fire and wipe out sibling checkboxes' mixed
            // state during multi-selection. Only cascade when definitely checked.
            if (this.chkSwitchDisplayMiddle.CheckState == CheckState.Checked)
            {
                this.chkSwitchDisplayLower.Checked = false;
                this.chkSwitchDisplayUpper.Checked = false;
            }
        }

        private void chkSwitchDisplayLower_CheckedChanged_1(object sender, EventArgs e)
        {
            // styd: .Checked is ALSO true for CheckState.Indeterminate in WinForms,
            // which made this cascade fire and wipe out sibling checkboxes' mixed
            // state during multi-selection. Only cascade when definitely checked.
            if (this.chkSwitchDisplayLower.CheckState == CheckState.Checked)
            {
                this.chkSwitchDisplayUpper.Checked = false;
                this.chkSwitchDisplayMiddle.Checked = false;
            }
        }

        private void chkSwitchTextureUpper_CheckedChanged_1(object sender, EventArgs e)
        {
            // styd: .Checked is ALSO true for CheckState.Indeterminate in WinForms,
            // which made this cascade fire and wipe out sibling checkboxes' mixed
            // state during multi-selection. Only cascade when definitely checked.
            if (this.chkSwitchTextureUpper.CheckState == CheckState.Checked)
            {
                this.chkSwitchTextureLower.Checked = false;
                this.chkSwitchTextureMiddle.Checked = false;
            }
        }

        private void chkSwitchTextureMiddle_CheckedChanged_1(object sender, EventArgs e)
        {
            // styd: .Checked is ALSO true for CheckState.Indeterminate in WinForms,
            // which made this cascade fire and wipe out sibling checkboxes' mixed
            // state during multi-selection. Only cascade when definitely checked.
            if (this.chkSwitchTextureMiddle.CheckState == CheckState.Checked)
            {
                this.chkSwitchTextureLower.Checked = false;
                this.chkSwitchTextureUpper.Checked = false;
            }
        }

        private void chkSwitchTextureLower_CheckedChanged_1(object sender, EventArgs e)
        {
            // styd: .Checked is ALSO true for CheckState.Indeterminate in WinForms,
            // which made this cascade fire and wipe out sibling checkboxes' mixed
            // state during multi-selection. Only cascade when definitely checked.
            if (this.chkSwitchTextureLower.CheckState == CheckState.Checked)
            {
                this.chkSwitchTextureUpper.Checked = false;
                this.chkSwitchTextureMiddle.Checked = false;
            }
        }
	}
}
