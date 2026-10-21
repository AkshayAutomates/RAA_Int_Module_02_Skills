using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RAA_Int_Module_02_Skills
{
    [Transaction(TransactionMode.Manual)]
    public class Command2 : IExternalCommand   // CHANGED: new command, Command1 is untouched
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Revit application and document variables
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // CHANGED: no more doc.ActiveView - we collect all the views that need tagging
            List<View> viewsToTag = GetViewsToTag(doc);

            // Cretion of List Of Catgeories
            List<BuiltInCategory> allCat = new List<BuiltInCategory>();
            allCat.Add(BuiltInCategory.OST_Areas);
            allCat.Add(BuiltInCategory.OST_Walls);
            allCat.Add(BuiltInCategory.OST_Doors);
            allCat.Add(BuiltInCategory.OST_Furniture);
            allCat.Add(BuiltInCategory.OST_LightingFixtures);
            allCat.Add(BuiltInCategory.OST_Rooms);
            allCat.Add(BuiltInCategory.OST_Windows);

            // Creation of Category Filter
            ElementMulticategoryFilter multicatFilter = new ElementMulticategoryFilter(allCat);

            // CHANGED: the 7 copy-pasted LINQ blocks and the dictionary are now in one method
            Dictionary<string, FamilySymbol> tags = GetTagSymbols(doc);

            using (Transaction t = new Transaction(doc))
            {
                t.Start("Insert Tags In All Views");   // CHANGED: transaction name
                int tagCount = 0;

                // CHANGED: loop through the views, the per-view code lives in TagViewElements
                foreach (View curView in viewsToTag)
                {
                    tagCount += TagViewElements(doc, curView, multicatFilter, tags);
                }

                t.Commit();

                // CHANGED: dialog moved after Commit
                TaskDialog.Show("Tags Placed", $"{tagCount} Tags are placed in all views");
            }
            return Result.Succeeded;

        }

        // CHANGED (new method): returns all the views that need to be tagged
        private List<View> GetViewsToTag(Document doc)
        {
            List<View> viewsToTag = new List<View>();

            FilteredElementCollector viewCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(View));

            foreach (View curView in viewCollector)
            {
                // skip view templates
                if (curView.IsTemplate)
                    continue;

                ViewType curViewType = curView.ViewType;

                if (curViewType == ViewType.FloorPlan ||
                    curViewType == ViewType.CeilingPlan ||
                    curViewType == ViewType.AreaPlan)
                {
                    viewsToTag.Add(curView);
                }
                else if (curViewType == ViewType.Section && curView.Name.Equals("Section Through Main Stair"))
                {
                    // only this one section view is tagged
                    viewsToTag.Add(curView);
                }
            }

            return viewsToTag;
        }

        // CHANGED (new method): replaces the repeated LINQ blocks + dictionary from Command1
        private Dictionary<string, FamilySymbol> GetTagSymbols(Document doc)
        {
            //LINQ to get Family Symbols
            FamilySymbol curtainWallTag = GetFamilySymbolByName(doc, "M_Curtain Wall Tag");
            FamilySymbol doorTag = GetFamilySymbolByName(doc, "M_Door Tag");
            FamilySymbol furnTag = GetFamilySymbolByName(doc, "M_Furniture Tag");
            FamilySymbol ltgFixTag = GetFamilySymbolByName(doc, "M_Lighting Fixture Tag");
            FamilySymbol roomTag = GetFamilySymbolByName(doc, "M_Room Tag");
            FamilySymbol wallTag = GetFamilySymbolByName(doc, "M_Wall Tag");
            FamilySymbol windowTag = GetFamilySymbolByName(doc, "M_Window Tag");

            //Creation of the Dictioneries
            Dictionary<string, FamilySymbol> tags = new Dictionary<string, FamilySymbol>();
            //tags.Add("Areas", areaTag); not required for tagging
            tags.Add("Curtain Walls", curtainWallTag);
            tags.Add("Doors", doorTag);
            tags.Add("Furniture", furnTag);
            tags.Add("Lighting Fixtures", ltgFixTag);
            tags.Add("Rooms", roomTag);
            tags.Add("Walls", wallTag);
            tags.Add("Windows", windowTag);

            return tags;
        }

        // CHANGED (new method): same LINQ as Command1, written once
        private FamilySymbol GetFamilySymbolByName(Document doc, string familyName)
        {
            FamilySymbol curSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.FamilyName.Equals(familyName))
                .First();

            return curSymbol;
        }

        // CHANGED (new method): your Command1 foreach loop, now runs for one view and returns the tag count
        private int TagViewElements(Document doc, View curView, ElementMulticategoryFilter multicatFilter, Dictionary<string, FamilySymbol> tags)
        {
            // CHANGED: collector is created per view
            FilteredElementCollector collector = new FilteredElementCollector(doc, curView.Id);
            collector.WherePasses(multicatFilter).WhereElementIsNotElementType();

            int tagCount = 0;   // CHANGED: counter for this view only, added to the main counter in Execute

            foreach (Element curElement in collector)
            {
                //Getting point from location
                XYZ insPoint;
                LocationPoint locPoint;
                LocationCurve locCurve;

                Location curLoc = curElement.Location;
                if (curLoc == null)
                    continue;

                locPoint = curLoc as LocationPoint;
                if (locPoint != null)
                {
                    //is location point
                    insPoint = locPoint.Point;
                }
                else
                {
                    //is location curve
                    locCurve = curLoc as LocationCurve;
                    Curve curCurve = locCurve.Curve;

                    insPoint = GetMidPointBetweenTwoPoints(curCurve.GetEndPoint(0), curCurve.GetEndPoint(1));
                }

                // Code is for only Area Tags as the logic nof independent tags not applied
                ViewType curViewType = curView.ViewType;
                if (curViewType == ViewType.AreaPlan)
                {
                    if (curElement.Category.Name == "Areas")
                    {
                        ViewPlan curAreaPlan = curView as ViewPlan;
                        Area curArea = curElement as Area;

                        AreaTag curAreaTag = doc.Create.NewAreaTag(curAreaPlan, curArea, new UV(insPoint.X, insPoint.Y));
                        curAreaTag.TagHeadPosition = new XYZ(insPoint.X, insPoint.Y, 0);
                        curAreaTag.HasLeader = false;
                        tagCount++;

                    }
                    else continue;

                }


                else if (curViewType == ViewType.CeilingPlan)
                {
                    if (curElement.Category.Name == "Lighting Fixtures" || curElement.Category.Name == "Rooms")
                    {
                        FamilySymbol curTagType = tags[curElement.Category.Name];

                        Reference curRef = new Reference(curElement);
                        IndependentTag newtag = IndependentTag.Create(doc, curTagType.Id, curView.Id, curRef, false, TagOrientation.Horizontal, insPoint);
                        tagCount++;
                    }

                }
                else if (curViewType == ViewType.FloorPlan)
                {
                    if (curElement.Category.Name == "Walls")
                    {
                        Wall curWall = curElement as Wall;
                        WallType curWallType = curWall.WallType;
                        if (curWallType.Kind == WallKind.Curtain)
                        {
                            FamilySymbol curTagType = tags["Curtain Walls"];

                            Reference curRef = new Reference(curElement);
                            IndependentTag newtag = IndependentTag.Create(doc, curTagType.Id, curView.Id, curRef, false, TagOrientation.Horizontal, insPoint);
                            tagCount++;
                        }
                        else
                        {
                            FamilySymbol curTagType = tags[curElement.Category.Name];

                            Reference curRef = new Reference(curElement);
                            IndependentTag newtag = IndependentTag.Create(doc, curTagType.Id, curView.Id, curRef, false, TagOrientation.Horizontal, insPoint);
                            tagCount++;
                        }
                    }
                    if (curElement.Category.Name == "Doors" ||
                        curElement.Category.Name == "Furniture" ||
                        curElement.Category.Name == "Rooms")
                    {
                        FamilySymbol curTagType = tags[curElement.Category.Name];

                        Reference curRef = new Reference(curElement);
                        IndependentTag newtag = IndependentTag.Create(doc, curTagType.Id, curView.Id, curRef, false, TagOrientation.Horizontal, insPoint);
                        tagCount++;
                    }
                    else if (curElement.Category.Name == "Windows")
                    {
                        FamilySymbol curTagType = tags[curElement.Category.Name];

                        Reference curRef = new Reference(curElement);
                        IndependentTag newtag = IndependentTag.Create(doc, curTagType.Id, curView.Id, curRef, false, TagOrientation.Horizontal, insPoint);
                        newtag.TagHeadPosition = new XYZ(insPoint.X, insPoint.Y + 3, insPoint.Z);
                        tagCount++;
                    }


                }
                else if (curViewType == ViewType.Section)
                {
                    if (curElement.Category.Name == "Rooms")

                    {
                        FamilySymbol curTagType = tags[curElement.Category.Name];

                        Reference curRef = new Reference(curElement);
                        IndependentTag newtag = IndependentTag.Create(doc, curTagType.Id, curView.Id, curRef, false, TagOrientation.Horizontal, insPoint);
                        newtag.TagHeadPosition = new XYZ(insPoint.X, insPoint.Y, insPoint.Z + 3);
                        newtag.HasLeader = false;
                        tagCount++;
                    }

                }


            }

            return tagCount;   // CHANGED: returns the count to Execute
        }

        private XYZ GetMidPointBetweenTwoPoints(XYZ point1, XYZ point2)
        {
            XYZ midPoint = new XYZ(
                (point1.X + point2.X) / 2,
                (point1.Y + point2.Y) / 2,
                (point1.Z + point2.Z) / 2);

            return midPoint;
        }
    }

}