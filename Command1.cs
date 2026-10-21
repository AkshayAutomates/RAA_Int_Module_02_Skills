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
    public class Command1 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Revit application and document variables
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Tagging the views as per category and cat tags.
            View curView = doc.ActiveView;

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

            FilteredElementCollector collector = new FilteredElementCollector(doc, curView.Id);
            collector.WherePasses(multicatFilter).WhereElementIsNotElementType();

            //LINQ to get Family Symbols
 
            FamilySymbol curtainWallTag = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.FamilyName.Equals("M_Curtain Wall Tag"))
                .First();

             FamilySymbol doorTag = new FilteredElementCollector(doc)
                 .OfClass(typeof(FamilySymbol))
                 .Cast<FamilySymbol>()
                 .Where(x => x.FamilyName.Equals("M_Door Tag"))
                 .First();

            FamilySymbol furnTag = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.FamilyName.Equals("M_Furniture Tag"))
                .First();

            FamilySymbol ltgFixTag = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.FamilyName.Equals("M_Lighting Fixture Tag"))
                .First();

            FamilySymbol roomTag = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.FamilyName.Equals("M_Room Tag"))
                .First();

            FamilySymbol wallTag = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.FamilyName.Equals("M_Wall Tag"))
                .First();

            FamilySymbol windowTag = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.FamilyName.Equals("M_Window Tag"))
                .First();

            //Creation of the Dictioneries
            Dictionary<string, FamilySymbol> tags = new Dictionary<string, FamilySymbol>();
            //tags.Add("Areas", areaTag); not required for tagging
            tags.Add("Curtain Walls", curtainWallTag);
            tags.Add("Doors", doorTag);
            tags.Add("Furniture", furnTag);
            tags.Add("Lighting Fixtures", ltgFixTag);
            tags.Add("Rooms", roomTag );
            tags.Add("Walls", wallTag);
            tags.Add("Windows", windowTag);

            using (Transaction t = new Transaction(doc))
            {
                t.Start("Insert Tags");
                int tagCount = 0;
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


                    else if (curViewType == ViewType.CeilingPlan )
                    {
                        if(curElement.Category.Name == "Lighting Fixtures" || curElement.Category.Name == "Rooms")
                        {
                            FamilySymbol curTagType = tags[curElement.Category.Name];

                            Reference curRef = new Reference(curElement);
                            IndependentTag newtag = IndependentTag.Create(doc, curTagType.Id, curView.Id, curRef, false, TagOrientation.Horizontal, insPoint);
                            tagCount++;
                        }

                    }
                    else if (curViewType == ViewType.FloorPlan)
                    {
                        if( curElement.Category.Name == "Walls")
                        {
                            Wall curWall = curElement as Wall;
                            WallType curWallType = curWall.WallType;
                            if(curWallType.Kind == WallKind.Curtain)
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
                            curElement.Category.Name == "Rooms" )   
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
                TaskDialog.Show("Tags Placed", $"{tagCount} Tags are placed in active View");
                t.Commit();
                //TaskDialog.Show("Test", $"Total {areaTagCount} Area Tags are created");
            }
            return Result.Succeeded;
        
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
