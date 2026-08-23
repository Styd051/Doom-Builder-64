
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
    // styd: searches/replaces sectors by light tags (the "Tag" field of
    // each slot in Edit Sector > Lights, the one used by P_ChangeLightByTag/
    // P_FindLightFromLightTag on the engine side - not the classic Sector.Tag, already covered
    // by "Sector Tags"). Third addition to the Sector Light family*: the index
    // finds "which physical slot", the color finds "which appearance", this one
    // finds "which animation/synchronization link".
    //
    // Same validation pattern as FindSectorTags.cs (the equivalent for Sector.Tag):
    // the search is not bounded by Min/MaxTag (a tag outside the bounds simply finds
    // nothing, it's not an error), the replacement is. //
    // The replacement ONLY changes the tag and preserves the existing RGB color of
    // each slot found - unlike FindSectorLightColor, there is only one
    // field to change here, not five potentially different colors to choose from.
    //
    // As with FindSectorLightColor, hasOriginalIndex is dropped: a slot whose
    // tag changes is no longer, by definition, the same input it
    // pointed to before (same reasoning as for a color change). isDirect
    // is automatically recalculated by the Lights(r,g,b,tag) constructor (see the
    // recent fix in Lights.cs): a new non-null tag becomes
    // correctly non-direct even if the color is gray.
    [FindReplace("Sector Light Tag", BrowseButton = false)]
	internal class FindSectorLightTag : FindReplaceType
	{
		#region ================== Constants

		#endregion

		#region ================== Variables

		#endregion

		#region ================== Constructor / Destructor

		// Constructor
		public FindSectorLightTag()
		{
			// Initialize

		}

		// Destructor
		~FindSectorLightTag()
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

		// This is called to perform a search (and replace)
		// Returns a list of items to show in the results list
		// replacewith is null when not replacing
		public override FindReplaceObject[] Find(string value, bool withinselection, string replacewith, bool keepselection)
		{
			List<FindReplaceObject> objs = new List<FindReplaceObject>();

			// Interpret the replacement, same bounds as FindSectorTags.cs (Sector.Tag)
			int replacetag = 0;
			if(replacewith != null)
			{
				// If it cannot be interpreted, set replacewith to null (not replacing at all)
				if(!int.TryParse(replacewith, out replacetag)) replacewith = null;
				if(replacetag < General.Map.FormatInterface.MinTag) replacewith = null;
				if(replacetag > General.Map.FormatInterface.MaxTag) replacewith = null;
				if(replacewith == null)
				{
					MessageBox.Show("Invalid replace value for this search type!", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return objs.ToArray();
				}
			}

			// Interpret the number given (not bounds-checked - an out-of-range tag simply
			// won't match anything, same as FindSectorTags.cs)
			int searchtag;
			if(!int.TryParse(value, out searchtag))
				return objs.ToArray();

			bool doreplace = (replacewith != null);
			UInt16 newtag = (UInt16)replacetag;

			// Where to search?
			ICollection<Sector> list = withinselection ? General.Map.Map.GetSelectedSectors(true) : General.Map.Map.Sectors;

			// Go for all sectors, all 5 color slots
			foreach(Sector s in list)
			{
				if(s.FloorColor.tag == searchtag)
				{
					if(doreplace) s.FloorColor = new Lights(s.FloorColor.color.r, s.FloorColor.color.g, s.FloorColor.color.b, newtag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Floor Color"));
				}

				if(s.CeilColor.tag == searchtag)
				{
					if(doreplace) s.CeilColor = new Lights(s.CeilColor.color.r, s.CeilColor.color.g, s.CeilColor.color.b, newtag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Ceiling Color"));
				}

				if(s.ThingColor.tag == searchtag)
				{
					if(doreplace) s.ThingColor = new Lights(s.ThingColor.color.r, s.ThingColor.color.g, s.ThingColor.color.b, newtag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Thing Color"));
				}

				if(s.TopColor.tag == searchtag)
				{
					if(doreplace) s.TopColor = new Lights(s.TopColor.color.r, s.TopColor.color.g, s.TopColor.color.b, newtag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Upper Wall Color"));
				}

				if(s.LowerColor.tag == searchtag)
				{
					if(doreplace) s.LowerColor = new Lights(s.LowerColor.color.r, s.LowerColor.color.g, s.LowerColor.color.b, newtag);
					objs.Add(new FindReplaceObject(s, "Sector " + s.Index + " Lower Wall Color"));
				}
			}

			// styd: same reason as FindSectorLightIndex/FindSectorLightColor - the color
			// property setters flag updateneeded (Sector.cs fix), but nothing walks dirty
			// sectors on a redraw by itself. MapSet.Update() flushes it before
			// FindReplaceForm's RedrawDisplay() call right after this method returns.
			// (A tag-only change doesn't actually affect the rendered color, but it's
			// cheap and keeps this class consistent with its two siblings.)
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
