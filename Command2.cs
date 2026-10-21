#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB.Architecture;

#endregion

namespace RAA_Int_Module_02_Skills
{
    [Transaction(TransactionMode.Manual)]
    public class Command2 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // this is a variable for the Revit application
            UIApplication uiapp = commandData.Application;

            // this is a variable for the current Revit model
            Document doc = uiapp.ActiveUIDocument.Document;
            // STEP 1: get all rooms
            List<Room> rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .OfType<Room>()
                .ToList();

            if (rooms.Count == 0)
            {
                TaskDialog.Show("Error", "No rooms found in this project.");
                return Result.Failed;
            }

            // STEP 2: get the unique department names
            List<string> departments = new List<string>();
            foreach (Room room in rooms)
            {
                string dept = room.LookupParameter("Department")?.AsString();
                departments.Add(dept);
            }
            List<string> deptList = departments.Distinct().ToList();

            // STEP 3: create the schedules
            int count = 0;

            using (Transaction t = new Transaction(doc, "Create room schedules"))
            {
                t.Start();

                // one schedule per department
                foreach (string dept in deptList)
                {
                    ViewSchedule sched = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Rooms));
                    sched.Name = "Department - " + dept;
                    ScheduleDefinition def = sched.Definition;

                    // add columns (order here = order in the schedule)
                    ScheduleField numberField = AddField(doc, def, "Number");
                    ScheduleField nameField = AddField(doc, def, "Name");
                    ScheduleField deptField = AddField(doc, def, "Department");
                    AddField(doc, def, "Comments");
                    ScheduleField areaField = AddField(doc, def, "Area");
                    ScheduleField levelField = AddField(doc, def, "Level");

                    // hide level (used only for grouping) and total the area
                    levelField.IsHidden = true;
                    areaField.DisplayType = ScheduleFieldDisplayType.Totals;

                    // filter: only this department
                    def.AddFilter(new ScheduleFilter(deptField.FieldId, ScheduleFilterType.Equal, dept));

                    // sort by level (with header/footer), then by room name
                    ScheduleSortGroupField levelSort = new ScheduleSortGroupField(levelField.FieldId);
                    levelSort.ShowHeader = true;
                    levelSort.ShowFooter = true;
                    levelSort.ShowBlankLine = true;
                    def.AddSortGroupField(levelSort);
                    def.AddSortGroupField(new ScheduleSortGroupField(nameField.FieldId));

                    // grand total
                    def.ShowGrandTotal = true;
                    def.ShowGrandTotalTitle = true;
                    def.ShowGrandTotalCount = true;

                    count++;
                }

                // one schedule for all departments (totals only)
                ViewSchedule allSched = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Rooms));
                allSched.Name = "All Departments";
                ScheduleDefinition allDef = allSched.Definition;

                ScheduleField allDeptField = AddField(doc, allDef, "Department");
                ScheduleField allAreaField = AddField(doc, allDef, "Area");
                allAreaField.DisplayType = ScheduleFieldDisplayType.Totals;

                allDef.AddSortGroupField(new ScheduleSortGroupField(allDeptField.FieldId));
                allDef.IsItemized = false;      // one row per department
                allDef.ShowGrandTotal = true;
                allDef.ShowGrandTotalTitle = true;

                t.Commit();
            }
            TaskDialog.Show("Done", "Created " + count + " department schedules + 1 summary schedule.");
            return Result.Succeeded;
        }
        // Finds a field by its name in Revit's list of allowed fields and adds it
        private ScheduleField AddField(Document doc, ScheduleDefinition def, string fieldName)
        {
            foreach (SchedulableField sf in def.GetSchedulableFields())
            {
                if (sf.GetName(doc) == fieldName)
                    return def.AddField(sf);
            }

            return null; // field not available
        }
    }
}