using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using OfficeOpenXml;

namespace RuknBoqMapper
{
    public static class RuknBoqExportService
    {
        public static void ExportElements(string filePath, Document doc, List<Element> elements)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Revit Elements");
                
                string[] headers = {
                    "Element ID", "Unique ID", "Category", "Family Name", "Type Name",
                    "Level", "Workset", "Mark", "Package No", "Bill No",
                    "System Code", "Page No", "Item No", "RUKN_5D_BOQ CODE"
                };

                // Define styling colors based on the Legend
                var colTitleColor = System.Drawing.Color.FromArgb(56, 56, 56); // #383838
                var valTypeColor = System.Drawing.Color.FromArgb(198, 224, 213); // #C6E0D5 Value type
                var lockedValueColor = System.Drawing.Color.FromArgb(217, 217, 217); // #D9D9D9 Parameter value locked (Element ID, Category etc)

                // Apply styling to 'Revit Elements' Headers
                ws.Row(1).Height = 24;
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cells[1, i + 1];
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(colTitleColor);
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                }

                int row = 2;
                foreach (var elem in elements)
                {
                    ws.Cells[row, 1].Value = elem.Id.ToString();
                    ws.Cells[row, 2].Value = elem.UniqueId;
                    ws.Cells[row, 3].Value = elem.Category?.Name ?? "Unknown";
                    
                    var typeId = elem.GetTypeId();
                    var typeElem = typeId != ElementId.InvalidElementId ? doc.GetElement(typeId) as ElementType : null;
                    ws.Cells[row, 4].Value = typeElem?.FamilyName ?? "";
                    ws.Cells[row, 5].Value = typeElem?.Name ?? elem.Name;

                    var levelId = elem.LevelId;
                    var level = levelId != ElementId.InvalidElementId ? doc.GetElement(levelId) as Level : null;
                    ws.Cells[row, 6].Value = level?.Name ?? "";

                    try
                    {
                        var worksetId = elem.WorksetId;
                        var workset = doc.GetWorksetTable()?.GetWorkset(worksetId);
                        ws.Cells[row, 7].Value = workset?.Name ?? "";
                    }
                    catch { ws.Cells[row, 7].Value = ""; }

                    // Apply locked value color for columns 1 to 7 (read-only/locked metadata)
                    for (int col = 1; col <= 7; col++)
                    {
                        ws.Cells[row, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        ws.Cells[row, col].Style.Fill.BackgroundColor.SetColor(lockedValueColor);
                    }

                    ws.Cells[row, 8].Value = elem.LookupParameter("Mark")?.AsString() ?? "";

                    ws.Cells[row, 9].Value = elem.LookupParameter("PACKAGE_NO")?.AsString() ?? "";
                    ws.Cells[row, 10].Value = elem.LookupParameter("BILL_NO")?.AsString() ?? "";
                    ws.Cells[row, 11].Value = elem.LookupParameter("SYSTEM_CODE")?.AsString() ?? "";
                    ws.Cells[row, 12].Value = elem.LookupParameter("PAGE_NO")?.AsString() ?? "";
                    ws.Cells[row, 13].Value = elem.LookupParameter("ITEM_NO")?.AsString() ?? "";
                    ws.Cells[row, 14].Value = elem.LookupParameter("RUKN_5D_BOQ CODE")?.AsString() ?? "";

                    // Set light green (#92D050) background for the RUKN_5D_BOQ CODE column (column 14) indicating it's write-locked/read-only
                    ws.Cells[row, 14].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[row, 14].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(146, 208, 80));

                    row++;
                }

                var wsMap = package.Workbook.Worksheets.Add("BOQ Mapping");
                string[] mapHeaders = {
                    "Category", "Family Name", "Type Name", "Package No", "Bill No", "System Code", "Page No", "Item No",
                    "District Code", "Asset Group", "Asset Type", "Location Code", "Description", "ABS L1", "ABS L2", "ABS L3"
                };

                // Apply styling to 'BOQ Mapping' Headers
                wsMap.Row(1).Height = 24;
                for (int i = 0; i < mapHeaders.Length; i++)
                {
                    var cell = wsMap.Cells[1, i + 1];
                    cell.Value = mapHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(colTitleColor);
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                }



                for (int i = 1; i <= headers.Length; i++)
                {
                    ws.Column(i).Width = 18;
                }
                for (int i = 1; i <= mapHeaders.Length; i++)
                {
                    wsMap.Column(i).Width = 18;
                }

                // Add Color Legend Worksheet
                var wsLegend = package.Workbook.Worksheets.Add("Color Legend");
                wsLegend.View.ShowGridLines = true;
                
                // Set column widths
                wsLegend.Column(1).Width = 15;
                wsLegend.Column(2).Width = 65;

                // Title
                wsLegend.Cells[1, 1, 1, 2].Merge = true;
                wsLegend.Cells[1, 1].Value = "Color legend";
                wsLegend.Cells[1, 1].Style.Font.Bold = true;
                wsLegend.Cells[1, 1].Style.Font.Size = 14;
                wsLegend.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                wsLegend.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                wsLegend.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Black);
                wsLegend.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                wsLegend.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                wsLegend.Row(1).Height = 28;

                // Column Headers
                wsLegend.Cells[2, 1].Value = "Color";
                wsLegend.Cells[2, 2].Value = "Description";
                for (int c = 1; c <= 2; c++)
                {
                    var cell = wsLegend.Cells[2, c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.Size = 12;
                    cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(64, 64, 64));
                    cell.Style.HorizontalAlignment = c == 1 ? OfficeOpenXml.Style.ExcelHorizontalAlignment.Center : OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                    cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                }
                wsLegend.Row(2).Height = 22;

                // Legend items: Color ARGB and Text Description
                var legendItems = new List<Tuple<System.Drawing.Color, string>>
                {
                    Tuple.Create(System.Drawing.Color.FromArgb(56, 56, 56), "Column title"),

                    Tuple.Create(System.Drawing.Color.FromArgb(217, 217, 217), "Parameter value locked and which cannot be modified for import"),
                    Tuple.Create(System.Drawing.Color.FromArgb(198, 239, 255), "Value of a locked item type parameter"),
                    Tuple.Create(System.Drawing.Color.FromArgb(227, 160, 39), "Value of a parameter which cannot be exported"),
                    Tuple.Create(System.Drawing.Color.White, "Changing the value of the parameter is allowed"),
                    Tuple.Create(System.Drawing.Color.FromArgb(146, 208, 80), "value of the parameter is not allowed write & read only"),
                    Tuple.Create(System.Drawing.Color.FromArgb(192, 192, 192), "Sub total level 2 and above"),
                    Tuple.Create(System.Drawing.Color.FromArgb(77, 77, 77), "Total")
                };

                for (int idx = 0; idx < legendItems.Count; idx++)
                {
                    int r = idx + 3;
                    var item = legendItems[idx];
                    wsLegend.Row(r).Height = 20;

                    // Color block cell
                    var colorCell = wsLegend.Cells[r, 1];
                    colorCell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    colorCell.Style.Fill.BackgroundColor.SetColor(item.Item1);

                    // Add thin borders inside the legend blocks
                    colorCell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                    // Description text cell
                    var descCell = wsLegend.Cells[r, 2];
                    descCell.Value = item.Item2;
                    descCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    descCell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }

                // Add Developer Credit Info
                int creditStartRow = legendItems.Count + 5;
                wsLegend.Cells[creditStartRow, 1, creditStartRow, 2].Merge = true;
                wsLegend.Cells[creditStartRow, 1].Value = "Add-in Prepared by: Ahmed Khalaf - BIM Manager";
                wsLegend.Cells[creditStartRow, 1].Style.Font.Bold = true;
                wsLegend.Cells[creditStartRow, 1].Style.Font.Size = 11;
                wsLegend.Cells[creditStartRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.Black);

                wsLegend.Cells[creditStartRow + 1, 1, creditStartRow + 1, 2].Merge = true;
                wsLegend.Cells[creditStartRow + 1, 1].Value = "Phone: 0542554127 | Email: engkhalaf7@gmail.com";
                wsLegend.Cells[creditStartRow + 1, 1].Style.Font.Italic = true;
                wsLegend.Cells[creditStartRow + 1, 1].Style.Font.Size = 10;

                package.SaveAs(new FileInfo(filePath));
            }
        }
    }
}
