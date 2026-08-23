
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
    // styd: searches/replaces sectors by color index LIGHTS, exclusive
    // Doom 64. Covers all 5 slots (Floor/Ceiling/Thing/Top Wall/Lower Wall) in a
    // single pass, just as FindSectorFlat already does for Floor/Ceiling textures.
    //
    // The searched value follows the exact same display convention as
    // Lights.GetDisplayIndex() (i.e., the Lights tab of Edit Sector and its
    // "Index" field): a value of 0-255 designates a direct gray intensity,
    // a value >= 256 designates the "256 + originalIndex" entry of the LIGHTS lump.
    //
    // The replacement never "guesses" a color: it copies the Lights (RGBA +
    // tag + originalIndex) from an existing slot elsewhere in the map that already uses
    // the target index. If this index does not yet exist anywhere in the map,
    // the replacement is refused with a clear message (you must first create
    // this color/index via Edit Sector > Lights).
    [FindReplace("Sector Light Index", BrowseButton = false)]
	internal class FindSectorLightIndex : FindReplaceType
	{
		#region ================== Constants

		#endregion

		#region ================== Variables

		#endregion

		#region ================== Constructor / Destructor

		// Constructor
		public FindSectorLightIndex()
		{
			// Initialize

		}

		// Destructor
		~FindSectorLightIndex()
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

		// This is called when the browse button is pressed
		public override string Browse(string initialvalue)
		{
			return "";
		}

		// styd: finds the first Lights value anywhere in the map (any sector, any of
		// the 5 slots) whose display index matches the given target. Used as the only
		// source of color data when replacing, so a replace can never invent a color
		// that doesn't already exist somewhere in the map.
		private static bool FindLightsByDisplayIndex(int targetindex, out Lights result)
		{
			string targetstr = targetindex.ToString();

			foreach(Sector s in General.Map.Map.Sectors)
			{
				if(Lights.GetDisplayIndex(s.FloorColor) == targetstr) { result = s.FloorColor; return true; }
				if(Lights.GetDisplayIndex(s.CeilColor) == targetstr) { result = s.CeilColor; return true; }
				if(Lights.GetDisplayIndex(s.ThingColor) == targetstr) { result = s.ThingColor; return true; }
				if(Lights.GetDisplayIndex(s.TopColor) == targetstr) { result = s.TopColor; return true; }
				if(Lights.GetDisplayIndex(s.LowerColor) == targetstr) { result = s.LowerColor; return true; }
			}

			result = new Lights();
			return false;
		}

		// This is called to perform a search (and replace)
		// Returns a list of items to show in the results list
		// replacewith is null when not replacing
		public override FindReplaceObject[] Find(string value, bool withinselection, string replacewith, bool keepselection)
		{
			List<FindReplaceObject> objs = new List<FindReplaceObject>();

			// Interpret the value to search for (same convention as Lights.GetDisplayIndex)
			int searchindex;
			if(!int.TryParse(value, out searchindex) || (searchindex < 0))
			{
				MessageBox.Show("Invalid search value for this search type! Enter the same index shown in Edit Sector > Lights (0-255 for a direct gray value, 256+ for a LIGHTS entry).", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return objs.ToArray();
			}
			string searchstr = searchindex.ToString();

			// Interpret the replacement, if any. The replacement must point to a light
			// value that already exists somewhere in the map — we never fabricate one.
			Lights replacelight = new Lights();
			bool doreplace = false;
			if(replacewith != null)
			{
				int replaceindex;
				if(int.TryParse(replacewith, out replaceindex) && (replaceindex >= 0) && FindLightsByDisplayIndex(replaceindex, out replacelight))
				{
					doreplace = true;
				}
				else
				{
					MessageBox.Show("Invalid replace value for this search type! The target index must already exist somewhere in the map (create it first via Edit Sector > Lights), then try again.", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
					replacewith = null;
				}
			}

			// Where to search?
			ICollection<Sector> list = withinselection ? General.Map.Map.GetSelectedSectors(true) : General.Map.Map.Sectors;

			// Go for all sectors, all 5 color slots
			foreach(Sector s in list)
			{
				if(Lights.GetDisplayIndex(s.FloorColor) == searchstr)
				{
					if(doreplace) s.FloorColor = replacelight;
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Floor Color"));
				}

				if(Lights.GetDisplayIndex(s.CeilColor) == searchstr)
				{
					if(doreplace) s.CeilColor = replacelight;
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Ceiling Color"));
				}

				if(Lights.GetDisplayIndex(s.ThingColor) == searchstr)
				{
					if(doreplace) s.ThingColor = replacelight;
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Thing Color"));
				}

				if(Lights.GetDisplayIndex(s.TopColor) == searchstr)
				{
					if(doreplace) s.TopColor = replacelight;
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Upper Wall Color"));
				}

				if(Lights.GetDisplayIndex(s.LowerColor) == searchstr)
				{
					if(doreplace) s.LowerColor = replacelight;
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Lower Wall Color"));
				}
			}

			// styd: setting the color properties (now correctly flagged updateneeded via the
			// Sector.cs fix) isn't enough by itself — nothing walks dirty sectors on a redraw.
			// MapSet.Update() is the documented call for "flush any pending sector cache
			// rebuilds" (see its own XML doc comment), already used by e.g. BrightnessMode
			// right after changing a sector property. FindReplaceForm calls RedrawDisplay()
			// right after Find() returns, so doing this here means the screen is already
			// correct by the time that redraw happens.
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
