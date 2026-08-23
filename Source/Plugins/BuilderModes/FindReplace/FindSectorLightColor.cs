
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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using CodeImp.DoomBuilder.Windows;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using System.Drawing;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Config;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
    // styd: searches/replaces sectors by RGB color (not by index), exclusive
    // Doom 64. Complementary to FindSectorLightIndex: index search finds
    // "which physical slot/LIGHTS" is used, color search finds "which
    // appearance" is used, regardless of the index or tag behind it. Two different
    // LIGHTS entries (different tags, for example) can have exactly the same
    // RGB without being the same "thing" in the index sense - only this search finds them both.
    //
    // Value = 6-digit hex "RRGGBB" (the # is allowed as a prefix), same convention
    // as ColorHandler.GetStringValue()/PixelColor.FromHex() elsewhere in the project.
    // A Browse button opens a true ColorDialog (same pattern as ColorHandler.Browse).
    //
    // The replacement constructs a Lights entry from the chosen RGB, preserving the
    // EXISTING tag of each found slot (no single global tag: Floor/Ceiling/
    // Thing/Top Wall/Lower Wall can each have a different tag on the same
    // sector). Unlike FindSectorLightIndex, it doesn't require an identical color
    // to already exist elsewhere on the map: AddLightGetIndex() (Doom64MapSetIO.cs)
    // already handles this case correctly when writing - a pure gray (r=g=b, tag=0) automatically becomes
    // a direct index again, otherwise a new LIGHTS entry is allocated
    // (and deduplicated with any entry already sharing the same RGB+tag).
    [FindReplace("Sector Light Color", BrowseButton = true)]
	internal class FindSectorLightColor : FindReplaceType
	{
		#region ================== Constants

		#endregion

		#region ================== Variables

		#endregion

		#region ================== Properties

		public override Image BrowseImage { get { return Properties.Resources.ColorPick; } }

		#endregion

		#region ================== Constructor / Destructor

		// Constructor
		public FindSectorLightColor()
		{
			// Initialize

		}

		// Destructor
		~FindSectorLightColor()
		{
		}

		#endregion

		#region ================== Methods

		// Only relevant for Doom 64 maps (Sector.FloorColor/CeilColor/etc. exist on
		// the generic Sector class, but are meaningless outside Doom64MapSetIO)
		public override bool DetermineVisiblity()
		{
			return General.Map.FormatInterface.InDoom64Mode;
		}

		// styd: same "RRGGBB" hex convention as ColorHandler.GetStringValue() elsewhere
		// in the project. Tolerates a leading '#' since that's how most people type a
		// hex color. Returns false (rather than throwing) on anything else, so callers
		// can show a clean error instead of an exception dialog.
		private static bool TryParseHexColor(string value, out PixelColor color)
		{
			string trimmed = (value ?? string.Empty).Trim().TrimStart('#');

			if(trimmed.Length == 6)
			{
				try
				{
					color = PixelColor.FromHex(trimmed);
					return true;
				}
				catch(FormatException) { }
			}

			color = new PixelColor();
			return false;
		}

		// This is called when the browse button is pressed
		public override string Browse(string initialvalue)
		{
			PixelColor initial;
			if(!TryParseHexColor(initialvalue, out initial))
				initial = new PixelColor(255, 128, 128, 128);

			ColorDialog dialog = new ColorDialog();
			dialog.AllowFullOpen = true;
			dialog.AnyColor = true;
			dialog.FullOpen = true;
			dialog.Color = Color.FromArgb(initial.r, initial.g, initial.b);

			if(dialog.ShowDialog(BuilderPlug.Me.FindReplaceForm) == DialogResult.OK)
			{
				Color c = dialog.Color;
				int rgb = (c.R << 16) | (c.G << 8) | c.B;
				return rgb.ToString("X6");
			}

			return initialvalue;
		}

		// styd: color-only comparison — deliberately ignores tag/isDirect/originalIndex.
		// Two entries with the same RGB but different tags (or different physical LIGHTS
		// slots) still count as the "same color" here; that's the whole point versus
		// FindSectorLightIndex.
		private static bool ColorMatches(Lights light, PixelColor target)
		{
			return (light.color.r == target.r) && (light.color.g == target.g) && (light.color.b == target.b);
		}

		// This is called to perform a search (and replace)
		// Returns a list of items to show in the results list
		// replacewith is null when not replacing
		public override FindReplaceObject[] Find(string value, bool withinselection, string replacewith, bool keepselection)
		{
			List<FindReplaceObject> objs = new List<FindReplaceObject>();

			// Interpret the value to search for
			PixelColor searchcolor;
			if(!TryParseHexColor(value, out searchcolor))
			{
				MessageBox.Show("Invalid search value for this search type! Enter a 6-digit hex color (e.g. FF8000), or use the browse button to pick one.", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return objs.ToArray();
			}

			// Interpret the replacement, if any. Unlike Sector Light Index, this never
			// needs to look anything up elsewhere in the map — a fresh RGB is always a
			// valid Lights value on its own.
			PixelColor replacecolor = new PixelColor();
			bool doreplace = false;
			if(replacewith != null)
			{
				if(TryParseHexColor(replacewith, out replacecolor))
				{
					doreplace = true;
				}
				else
				{
					MessageBox.Show("Invalid replace value for this search type! Enter a 6-digit hex color (e.g. FF8000), or use the browse button to pick one.", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
					replacewith = null;
				}
			}

			// Where to search?
			ICollection<Sector> list = withinselection ? General.Map.Map.GetSelectedSectors(true) : General.Map.Map.Sectors;

			// Go for all sectors, all 5 color slots
			foreach(Sector s in list)
			{
				if(ColorMatches(s.FloorColor, searchcolor))
				{
					if(doreplace) s.FloorColor = new Lights(replacecolor.r, replacecolor.g, replacecolor.b, s.FloorColor.tag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Floor Color"));
				}

				if(ColorMatches(s.CeilColor, searchcolor))
				{
					if(doreplace) s.CeilColor = new Lights(replacecolor.r, replacecolor.g, replacecolor.b, s.CeilColor.tag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Ceiling Color"));
				}

				if(ColorMatches(s.ThingColor, searchcolor))
				{
					if(doreplace) s.ThingColor = new Lights(replacecolor.r, replacecolor.g, replacecolor.b, s.ThingColor.tag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Thing Color"));
				}

				if(ColorMatches(s.TopColor, searchcolor))
				{
					if(doreplace) s.TopColor = new Lights(replacecolor.r, replacecolor.g, replacecolor.b, s.TopColor.tag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Upper Wall Color"));
				}

				if(ColorMatches(s.LowerColor, searchcolor))
				{
					if(doreplace) s.LowerColor = new Lights(replacecolor.r, replacecolor.g, replacecolor.b, s.LowerColor.tag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Lower Wall Color"));
				}
			}

			// styd: see FindSectorLightIndex for why this is required — the color property
			// setters flag updateneeded (Sector.cs fix), but nothing walks dirty sectors on
			// a redraw by itself. MapSet.Update() flushes it before FindReplaceForm's
			// RedrawDisplay() call right after this method returns.
			if(doreplace) General.Map.Map.Update();

			return objs.ToArray();
		}

		// This is called when a specific object is selected from the list
		public override void ObjectSelected(FindReplaceObject[] selection)
		{
			if(selection.Length == 1)
			{
				ZoomToSelection(selection);
				General.Interface.ShowSectorInfo(selection[0].Sector);
			}
			else
				General.Interface.HideInfo();

			General.Map.Map.ClearAllSelected();
			foreach(FindReplaceObject obj in selection) obj.Sector.Selected = true;
		}

		// Render selection
		public override void PlotSelection(IRenderer2D renderer, FindReplaceObject[] selection)
		{
			foreach(FindReplaceObject o in selection)
			{
				foreach(Sidedef sd in o.Sector.Sidedefs)
				{
					renderer.PlotLinedef(sd.Line, General.Colors.Selection);
				}
			}
		}

		// Edit objects
		public override void EditObjects(FindReplaceObject[] selection)
		{
			List<Sector> sectors = new List<Sector>(selection.Length);
			foreach(FindReplaceObject o in selection) sectors.Add(o.Sector);
			General.Interface.ShowEditSectors(sectors);
		}

		#endregion
	}
}
