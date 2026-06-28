using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace RuknBoqMapper
{
    public static class RuknBoqAuditExporter
    {
        public static void ExportToExcel(string filePath, List<AuditRecord> records)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Revit Elements");
                
                string[] headers = {
                    "Element ID", "Unique ID", "Category", "Family Name", "Type Name",
                    "Level", "Workset", "Mark", "Package No", "Bill No",
                    "System Code", "Page No", "Item No", "QIC_5D_BOQ_CODE",
                    "Status", "Remarks"
                };

                // Define styling colors based on the Legend
                var colTitleColor = System.Drawing.Color.FromArgb(56, 56, 56); // #383838
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
                foreach (var rec in records)
                {
                    ws.Cells[row, 1].Value = rec.ElementId;
                    ws.Cells[row, 2].Value = rec.UniqueId;
                    ws.Cells[row, 3].Value = rec.Category;
                    ws.Cells[row, 4].Value = rec.FamilyName;
                    ws.Cells[row, 5].Value = rec.TypeName;
                    ws.Cells[row, 6].Value = rec.Level;
                    ws.Cells[row, 7].Value = rec.Workset;

                    // Apply locked value color for columns 1 to 7 (read-only/locked metadata)
                    for (int col = 1; col <= 7; col++)
                    {
                        ws.Cells[row, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        ws.Cells[row, col].Style.Fill.BackgroundColor.SetColor(lockedValueColor);
                    }

                    ws.Cells[row, 8].Value = rec.Mark;
                    ws.Cells[row, 9].Value = rec.PackageNo;
                    ws.Cells[row, 10].Value = rec.BillNo;
                    ws.Cells[row, 11].Value = rec.SystemCode;
                    ws.Cells[row, 12].Value = rec.PageNo;
                    ws.Cells[row, 13].Value = rec.ItemNo;
                    ws.Cells[row, 14].Value = rec.GeneratedBoqCode;

                    // Set light green (#92D050) background for the QIC_5D_BOQ_CODE column (column 14) indicating it's write-locked/read-only
                    ws.Cells[row, 14].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[row, 14].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(146, 208, 80));

                    ws.Cells[row, 15].Value = rec.Status;
                    ws.Cells[row, 16].Value = rec.Remarks;

                    row++;
                }

                for (int i = 1; i <= headers.Length; i++)
                {
                    ws.Column(i).Width = 18;
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
                
                // Line 1: Add-in Prepared by: Ahmed Khalaf - BIM Manager
                wsLegend.Cells[creditStartRow, 1, creditStartRow, 2].Merge = true;
                wsLegend.Cells[creditStartRow, 1].Value = "Add-in Prepared by: Ahmed Khalaf - BIM Manager";
                wsLegend.Cells[creditStartRow, 1].Style.Font.Bold = true;
                wsLegend.Cells[creditStartRow, 1].Style.Font.Size = 11;
                wsLegend.Cells[creditStartRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.Black);

                // Line 2: Add-in Prepared by: RUKN BIM
                wsLegend.Cells[creditStartRow + 1, 1, creditStartRow + 1, 2].Merge = true;
                wsLegend.Cells[creditStartRow + 1, 1].Value = "Add-in Prepared by: RUKN BIM";
                wsLegend.Cells[creditStartRow + 1, 1].Style.Font.Bold = true;
                wsLegend.Cells[creditStartRow + 1, 1].Style.Font.Size = 11;
                wsLegend.Cells[creditStartRow + 1, 1].Style.Font.Color.SetColor(System.Drawing.Color.Black);

                // Line 3: Website: www.ruknbim.com
                wsLegend.Cells[creditStartRow + 2, 1, creditStartRow + 2, 2].Merge = true;
                wsLegend.Cells[creditStartRow + 2, 1].Value = "Website: www.ruknbim.com";
                wsLegend.Cells[creditStartRow + 2, 1].Style.Font.Size = 11;
                wsLegend.Cells[creditStartRow + 2, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(5, 99, 193)); // blue hyperlink
                wsLegend.Cells[creditStartRow + 2, 1].Style.Font.UnderLine = true;
                try { wsLegend.Cells[creditStartRow + 2, 1].Hyperlink = new Uri("http://www.ruknbim.com"); } catch { }

                // Line 4: Email: info@ruknbim.com
                wsLegend.Cells[creditStartRow + 3, 1, creditStartRow + 3, 2].Merge = true;
                wsLegend.Cells[creditStartRow + 3, 1].Value = "Email: info@ruknbim.com";
                wsLegend.Cells[creditStartRow + 3, 1].Style.Font.Size = 11;
                wsLegend.Cells[creditStartRow + 3, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(5, 99, 193)); // blue hyperlink
                wsLegend.Cells[creditStartRow + 3, 1].Style.Font.UnderLine = true;
                try { wsLegend.Cells[creditStartRow + 3, 1].Hyperlink = new Uri("mailto:info@ruknbim.com"); } catch { }

                // Line 5: Phone: 0542554127 | Email: engkhalaf7@gmail.com
                wsLegend.Cells[creditStartRow + 4, 1, creditStartRow + 4, 2].Merge = true;
                wsLegend.Cells[creditStartRow + 4, 1].Value = "Phone: 0542554127 | Email: engkhalaf7@gmail.com";
                wsLegend.Cells[creditStartRow + 4, 1].Style.Font.Italic = true;
                wsLegend.Cells[creditStartRow + 4, 1].Style.Font.Size = 10;

                package.SaveAs(new FileInfo(filePath));
            }
        }

        public static void ExportToCsv(string filePath, List<AuditRecord> records)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Element Id,Unique ID,Category,Family Name,Type Name,Package No,Bill No,System Code,Page No,Item No,Generated BOQ Code,Status,Remarks");

            foreach (var rec in records)
            {
                sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\",\"{11}\",\"{12}\"",
                    EscapeCsv(rec.ElementId),
                    EscapeCsv(rec.UniqueId),
                    EscapeCsv(rec.Category),
                    EscapeCsv(rec.FamilyName),
                    EscapeCsv(rec.TypeName),
                    EscapeCsv(rec.PackageNo),
                    EscapeCsv(rec.BillNo),
                    EscapeCsv(rec.SystemCode),
                    EscapeCsv(rec.PageNo),
                    EscapeCsv(rec.ItemNo),
                    EscapeCsv(rec.GeneratedBoqCode),
                    EscapeCsv(rec.Status),
                    EscapeCsv(rec.Remarks)
                ));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static void ExportToJson(string filePath, List<AuditRecord> records)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                sb.AppendLine("  {");
                sb.AppendLine(string.Format("    \"ElementId\": \"{0}\",", EscapeJson(rec.ElementId)));
                sb.AppendLine(string.Format("    \"UniqueId\": \"{0}\",", EscapeJson(rec.UniqueId)));
                sb.AppendLine(string.Format("    \"Category\": \"{0}\",", EscapeJson(rec.Category)));
                sb.AppendLine(string.Format("    \"FamilyName\": \"{0}\",", EscapeJson(rec.FamilyName)));
                sb.AppendLine(string.Format("    \"TypeName\": \"{0}\",", EscapeJson(rec.TypeName)));
                sb.AppendLine(string.Format("    \"PackageNo\": \"{0}\",", EscapeJson(rec.PackageNo)));
                sb.AppendLine(string.Format("    \"BillNo\": \"{0}\",", EscapeJson(rec.BillNo)));
                sb.AppendLine(string.Format("    \"SystemCode\": \"{0}\",", EscapeJson(rec.SystemCode)));
                sb.AppendLine(string.Format("    \"PageNo\": \"{0}\",", EscapeJson(rec.PageNo)));
                sb.AppendLine(string.Format("    \"ItemNo\": \"{0}\",", EscapeJson(rec.ItemNo)));
                sb.AppendLine(string.Format("    \"GeneratedBoqCode\": \"{0}\",", EscapeJson(rec.GeneratedBoqCode)));
                sb.AppendLine(string.Format("    \"Status\": \"{0}\",", EscapeJson(rec.Status)));
                sb.AppendLine(string.Format("    \"Remarks\": \"{0}\"", EscapeJson(rec.Remarks)));
                sb.Append("  }" + (i < records.Count - 1 ? "," : "") + Environment.NewLine);
            }
            sb.AppendLine("]");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string val)
        {
            if (val == null) return "";
            return val.Replace("\"", "\"\"");
        }

        private static string EscapeJson(string val)
        {
            if (val == null) return "";
            return val.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
