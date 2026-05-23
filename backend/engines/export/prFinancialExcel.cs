using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BimasaktiReports.FinancialReports.Backend.Engines
{
    public class ExcelReportPayload
    {
        public LedgerReportData? CurrData { get; set; }
        public LedgerReportData? PrevMoData { get; set; }
        public LedgerReportData? PrevYrData { get; set; }
        public List<LedgerReportData>? QuarterlyData { get; set; }
        public List<LedgerReportData>? YearlyData { get; set; }
        public string Company { get; set; } = "Company";
        public string Year { get; set; } = "";
        public string PeriodName { get; set; } = "";
        public string LastMonthName { get; set; } = "";
        public string LastMonthYear { get; set; } = "";
        public string LastYearName { get; set; } = "";
        public string LastYearYear { get; set; } = "";
    }

    public class LedgerReportData
    {
        public Dictionary<string, LedgerSectionData>? Structure { get; set; }
        public decimal NetIncome { get; set; }
    }

    public class LedgerSectionData
    {
        public decimal Total { get; set; }
        public Dictionary<string, LedgerGroupData>? Groups { get; set; }
    }

    public class LedgerGroupData
    {
        public decimal Total { get; set; }
        public List<LedgerItemData>? Items { get; set; }
    }

    public class LedgerItemData
    {
        public string No { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Balance { get; set; }
    }

    public static class prFinancialExcel
    {
        public static MemoryStream CreateExcelReport(ExcelReportPayload payload)
        {
            var currData = payload.CurrData;
            var prevMoData = payload.PrevMoData;
            var prevYrData = payload.PrevYrData;

            var quarterlyData = payload.QuarterlyData ?? new List<LedgerReportData>();
            var yearlyData = payload.YearlyData ?? new List<LedgerReportData>();

            string company = payload.Company;
            string year = payload.Year;
            string periodName = payload.PeriodName;

            using var workbook = new XLWorkbook();

            // --- SHEET 1: BS Summary ---
            var wsBs = workbook.Worksheets.Add("BS Summary");
            SetupSheet(wsBs);
            MergeRange(wsBs, "A1:E1", company, TitleFmt);
            MergeRange(wsBs, "A2:E2", "Balance Sheet Summary", SubtitleFmt);
            MergeRange(wsBs, "A3:E3", $"As of {periodName} {year}", SubtitleFmt);
            SetColumnWidth(wsBs, "A", 40);
            SetColumnWidth(wsBs, "B", 20);
            SetColumnWidth(wsBs, "C", 5);
            SetColumnWidth(wsBs, "D", 40);
            SetColumnWidth(wsBs, "E", 20);

            var structure = currData?.Structure ?? new Dictionary<string, LedgerSectionData>();

            structure.TryGetValue("Assets", out var assetsSec);
            structure.TryGetValue("Liabilities", out var liabSec);
            structure.TryGetValue("Equity", out var eqSec);

            int assetsEnd = WriteStandardSection(wsBs, assetsSec, 5, 0, "ASSETS", "BALANCE");
            int liabEnd = WriteStandardSection(wsBs, liabSec, 5, 3, "LIABILITIES", "BALANCE");

            Write(wsBs, liabEnd, 3, "TOTAL LIABILITIES", SubtotalLblFmt);
            Write(wsBs, liabEnd, 4, liabSec?.Total ?? 0, SubtotalNumFmt);

            int eqEnd = WriteStandardSection(wsBs, eqSec, liabEnd + 2, 3, "EQUITY", "");
            Write(wsBs, eqEnd, 3, "Current Year Earnings", ItemFmt);
            Write(wsBs, eqEnd, 4, currData?.NetIncome ?? 0, NumFmt);

            decimal eqTotal = (eqSec?.Total ?? 0) + (currData?.NetIncome ?? 0);
            Write(wsBs, eqEnd + 1, 3, "TOTAL EQUITY", SubtotalLblFmt);
            Write(wsBs, eqEnd + 1, 4, eqTotal, SubtotalNumFmt);

            int maxRow = Math.Max(assetsEnd, eqEnd + 2) + 1;
            Write(wsBs, maxRow, 0, "TOTAL ASSETS", TotalLblFmt);
            Write(wsBs, maxRow, 1, assetsSec?.Total ?? 0, TotalNumFmt);
            Write(wsBs, maxRow, 3, "TOTAL LIABILITIES & EQUITY", TotalLblFmt);
            Write(wsBs, maxRow, 4, (liabSec?.Total ?? 0) + eqTotal, TotalNumFmt);

            // --- SHEET 2: IS Summary ---
            var wsIs = workbook.Worksheets.Add("IS Summary");
            SetupSheet(wsIs);
            MergeRange(wsIs, "A1:E1", company, TitleFmt);
            MergeRange(wsIs, "A2:E2", "Income Statement Summary", SubtitleFmt);
            MergeRange(wsIs, "A3:E3", $"For the Period Ended {periodName} {year}", SubtitleFmt);
            SetColumnWidth(wsIs, "A", 40);
            SetColumnWidth(wsIs, "B", 20);
            SetColumnWidth(wsIs, "C", 5);
            SetColumnWidth(wsIs, "D", 40);
            SetColumnWidth(wsIs, "E", 20);

            structure.TryGetValue("Revenue", out var revSec);
            structure.TryGetValue("Expenses", out var expSec);

            int revEnd = WriteStandardSection(wsIs, revSec, 5, 0, "REVENUE", "ACTUAL");
            int expEnd = WriteStandardSection(wsIs, expSec, 5, 3, "EXPENSES", "ACTUAL");

            int maxRowIs = Math.Max(revEnd, expEnd) + 1;
            Write(wsIs, maxRowIs, 0, "TOTAL REVENUE", TotalLblFmt);
            Write(wsIs, maxRowIs, 1, revSec?.Total ?? 0, TotalNumFmt);
            Write(wsIs, maxRowIs, 3, "TOTAL EXPENSES", TotalLblFmt);
            Write(wsIs, maxRowIs, 4, expSec?.Total ?? 0, TotalNumFmt);

            // --- SHEET 3 & 4: Month-over-Month Comparison ---
            var compDatasets = new List<LedgerReportData?> { currData, prevMoData, prevYrData };
            var compHeaders = new List<string>
            {
                $"{periodName} {year}",
                $"{payload.LastMonthName ?? ""} {payload.LastMonthYear ?? ""}".Trim(),
                $"{payload.LastYearName ?? ""} {payload.LastYearYear ?? ""}".Trim()
            };

            // BS Details
            var wsBsc = workbook.Worksheets.Add("BS Detail Comparison");
            SetupSheet(wsBsc);
            MergeRange(wsBsc, "A1:D1", company, TitleFmt);
            MergeRange(wsBsc, "A2:D2", "Balance Sheet - Detailed Comparison", SubtitleFmt);
            SetColumnWidth(wsBsc, "A", 40);
            SetColumnWidth(wsBsc, "B:D", 18);

            Write(wsBsc, 4, 0, "ACCOUNT", HeaderLblFmt);
            for (int i = 0; i < compHeaders.Count; i++)
            {
                Write(wsBsc, 4, i + 1, compHeaders[i], HeaderFmt);
            }

            var (rAssets, assetsT) = WriteMultiCompSection(wsBsc, "Assets", 5, compDatasets);
            var (rLiab, liabT) = WriteMultiCompSection(wsBsc, "Liabilities", rAssets, compDatasets);
            var (rEq, eqT) = WriteMultiCompSection(wsBsc, "Equity", rLiab, compDatasets, isEquity: true);

            Write(wsBsc, rEq, 0, "TOTAL LIABILITIES & EQUITY", TotalLblFmt);
            for (int i = 0; i < 3; i++)
            {
                Write(wsBsc, rEq, i + 1, liabT[i] + eqT[i], TotalNumFmt);
            }

            // IS Details
            var wsIsc = workbook.Worksheets.Add("IS Detail Comparison");
            SetupSheet(wsIsc);
            MergeRange(wsIsc, "A1:D1", company, TitleFmt);
            MergeRange(wsIsc, "A2:D2", "Income Statement - Detailed Comparison", SubtitleFmt);
            SetColumnWidth(wsIsc, "A", 40);
            SetColumnWidth(wsIsc, "B:D", 18);

            Write(wsIsc, 4, 0, "ACCOUNT", HeaderLblFmt);
            for (int i = 0; i < compHeaders.Count; i++)
            {
                Write(wsIsc, 4, i + 1, compHeaders[i], HeaderFmt);
            }

            var (rRev, revT) = WriteMultiCompSection(wsIsc, "Revenue", 5, compDatasets);
            var (rExp, expT) = WriteMultiCompSection(wsIsc, "Expenses", rRev, compDatasets);

            Write(wsIsc, rExp, 0, "NET INCOME", TotalLblFmt);
            for (int i = 0; i < 3; i++)
            {
                decimal ni = compDatasets[i]?.NetIncome ?? 0;
                Write(wsIsc, rExp, i + 1, ni, TotalNumFmt);
            }

            // --- SHEET 5 & 6: QUARTERLY REPORTS ---
            if (quarterlyData != null && quarterlyData.Count == 4)
            {
                var qHeaders = new List<string> { "Q1 (Mar)", "Q2 (Jun)", "Q3 (Sep)", "Q4 (Dec)" };
                var qDatasets = quarterlyData.Cast<LedgerReportData?>().ToList();

                // BS Quarterly
                var wsQbs = workbook.Worksheets.Add("BS Quarterly");
                SetupSheet(wsQbs);
                MergeRange(wsQbs, "A1:E1", company, TitleFmt);
                MergeRange(wsQbs, "A2:E2", $"Balance Sheet - Quarterly Details ({year})", SubtitleFmt);
                SetColumnWidth(wsQbs, "A", 40);
                SetColumnWidth(wsQbs, "B:E", 15);

                Write(wsQbs, 4, 0, "ACCOUNT", HeaderLblFmt);
                for (int i = 0; i < qHeaders.Count; i++)
                {
                    Write(wsQbs, 4, i + 1, qHeaders[i], HeaderFmt);
                }

                var (rQAssets, qAssetsT) = WriteMultiCompSection(wsQbs, "Assets", 5, qDatasets);
                var (rQLiab, qLiabT) = WriteMultiCompSection(wsQbs, "Liabilities", rQAssets, qDatasets);
                var (rQEq, qEqT) = WriteMultiCompSection(wsQbs, "Equity", rQLiab, qDatasets, isEquity: true);

                Write(wsQbs, rQEq, 0, "TOTAL LIABILITIES & EQUITY", TotalLblFmt);
                for (int i = 0; i < 4; i++)
                {
                    Write(wsQbs, rQEq, i + 1, qLiabT[i] + qEqT[i], TotalNumFmt);
                }

                // IS Quarterly
                var wsQis = workbook.Worksheets.Add("IS Quarterly");
                SetupSheet(wsQis);
                MergeRange(wsQis, "A1:E1", company, TitleFmt);
                MergeRange(wsQis, "A2:E2", $"Income Statement - Quarterly Details ({year})", SubtitleFmt);
                SetColumnWidth(wsQis, "A", 40);
                SetColumnWidth(wsQis, "B:E", 15);

                Write(wsQis, 4, 0, "ACCOUNT", HeaderLblFmt);
                for (int i = 0; i < qHeaders.Count; i++)
                {
                    Write(wsQis, 4, i + 1, qHeaders[i], HeaderFmt);
                }

                var (rQRev, qRevT) = WriteMultiCompSection(wsQis, "Revenue", 5, qDatasets);
                var (rQExp, qExpT) = WriteMultiCompSection(wsQis, "Expenses", rQRev, qDatasets);

                Write(wsQis, rQExp, 0, "NET INCOME", TotalLblFmt);
                for (int i = 0; i < 4; i++)
                {
                    decimal ni = quarterlyData[i]?.NetIncome ?? 0;
                    Write(wsQis, rQExp, i + 1, ni, TotalNumFmt);
                }
            }

            // --- SHEET 7 & 8: YEARLY REPORTS ---
            if (yearlyData != null && yearlyData.Count == 12)
            {
                var yHeaders = new List<string>
                {
                    "January", "February", "March", "April", "May", "June",
                    "July", "August", "September", "October", "November", "December"
                };
                var yDatasets = yearlyData.Cast<LedgerReportData?>().ToList();

                // BS Yearly
                var wsYbs = workbook.Worksheets.Add("BS Yearly");
                SetupSheet(wsYbs);
                MergeRange(wsYbs, "A1:M1", company, TitleFmt);
                MergeRange(wsYbs, "A2:M2", $"Balance Sheet - Yearly Details ({year})", SubtitleFmt);
                SetColumnWidth(wsYbs, "A", 35);
                SetColumnWidth(wsYbs, "B:M", 13);

                Write(wsYbs, 4, 0, "ACCOUNT", HeaderLblFmt);
                for (int i = 0; i < yHeaders.Count; i++)
                {
                    Write(wsYbs, 4, i + 1, yHeaders[i], HeaderFmt);
                }

                var (rYAssets, yAssetsT) = WriteMultiCompSection(wsYbs, "Assets", 5, yDatasets);
                var (rYLiab, yLiabT) = WriteMultiCompSection(wsYbs, "Liabilities", rYAssets, yDatasets);
                var (rYEq, yEqT) = WriteMultiCompSection(wsYbs, "Equity", rYLiab, yDatasets, isEquity: true);

                Write(wsYbs, rYEq, 0, "TOTAL LIABILITIES & EQUITY", TotalLblFmt);
                for (int i = 0; i < 12; i++)
                {
                    Write(wsYbs, rYEq, i + 1, yLiabT[i] + yEqT[i], TotalNumFmt);
                }

                // IS Yearly
                var wsYis = workbook.Worksheets.Add("IS Yearly");
                SetupSheet(wsYis);
                MergeRange(wsYis, "A1:M1", company, TitleFmt);
                MergeRange(wsYis, "A2:M2", $"Income Statement - Yearly Details ({year})", SubtitleFmt);
                SetColumnWidth(wsYis, "A", 35);
                SetColumnWidth(wsYis, "B:M", 13);

                Write(wsYis, 4, 0, "ACCOUNT", HeaderLblFmt);
                for (int i = 0; i < yHeaders.Count; i++)
                {
                    Write(wsYis, 4, i + 1, yHeaders[i], HeaderFmt);
                }

                var (rYRev, yRevT) = WriteMultiCompSection(wsYis, "Revenue", 5, yDatasets);
                var (rYExp, yExpT) = WriteMultiCompSection(wsYis, "Expenses", rYRev, yDatasets);

                Write(wsYis, rYExp, 0, "NET INCOME", TotalLblFmt);
                for (int i = 0; i < 12; i++)
                {
                    decimal ni = yearlyData[i]?.NetIncome ?? 0;
                    Write(wsYis, rYExp, i + 1, ni, TotalNumFmt);
                }
            }

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        // ==========================================
        // HELPERS
        // ==========================================

        private static void SetupSheet(IXLWorksheet ws)
        {
            ws.ShowGridLines = false;
            ws.Protect("").AllowElement(XLSheetProtectionElements.FormatColumns).AllowElement(XLSheetProtectionElements.FormatRows);
        }

        private static void SetColumnWidth(IXLWorksheet ws, string colRange, double width)
        {
            if (colRange.Contains(':'))
            {
                var parts = colRange.Split(':');
                ws.Columns(parts[0], parts[1]).Width = width;
            }
            else
            {
                ws.Column(colRange).Width = width;
            }
        }

        private static void MergeRange(IXLWorksheet ws, string rangeStr, string value, Action<IXLStyle> styleAction)
        {
            var range = ws.Range(rangeStr);
            range.Merge();
            range.Value = value;
            styleAction(range.Style);
        }

        private static void Write(IXLWorksheet ws, int row, int col, object? value, Action<IXLStyle> styleAction)
        {
            var cell = ws.Cell(row + 1, col + 1);
            if (value == null)
            {
                cell.Value = Blank.Value;
            }
            else if (value is decimal d)
            {
                cell.SetValue(d);
            }
            else if (value is double db)
            {
                cell.SetValue(db);
            }
            else if (value is int intVal)
            {
                cell.SetValue(intVal);
            }
            else
            {
                cell.SetValue(value.ToString());
            }
            styleAction(cell.Style);
        }

        private static int WriteStandardSection(IXLWorksheet ws, LedgerSectionData? sectionData, int startRow, int startCol, string title, string valHeader = "BALANCE")
        {
            Write(ws, startRow, startCol, title.ToUpper(), HeaderLblFmt);
            Write(ws, startRow, startCol + 1, valHeader, HeaderFmt);
            int r = startRow + 1;

            if (sectionData == null) return r;

            var groups = sectionData.Groups ?? new Dictionary<string, LedgerGroupData>();

            // Deep Sort: Find absolute lowest account number in each group
            string GetMinAcc(string g)
            {
                string minAcc = "999999999";
                if (groups.TryGetValue(g, out var grpData) && grpData.Items != null)
                {
                    foreach (var item in grpData.Items)
                    {
                        if (string.Compare(item.No, minAcc) < 0)
                        {
                            minAcc = item.No;
                        }
                    }
                }
                return minAcc != "999999999" ? minAcc : g;
            }

            var sortedGroupNames = groups.Keys.OrderBy(GetMinAcc).ToList();

            foreach (var gName in sortedGroupNames)
            {
                var grp = groups[gName];
                Write(ws, r, startCol, gName, GroupFmt);
                Write(ws, r, startCol + 1, grp.Total, GroupNumFmt);
                r++;
            }

            return r;
        }

        private static (int nextRow, List<decimal> sectionTotals) WriteMultiCompSection(
            IXLWorksheet ws,
            string secName,
            int startRow,
            List<LedgerReportData?> datasets,
            bool isEquity = false)
        {
            Write(ws, startRow, 0, secName.ToUpper(), HeaderLblFmt);
            for (int i = 0; i < datasets.Count; i++)
            {
                Write(ws, startRow, i + 1, "", HeaderFmt);
            }
            int r = startRow + 1;

            var allGroups = new HashSet<string>();
            foreach (var ds in datasets)
            {
                if (ds != null && ds.Structure != null && ds.Structure.TryGetValue(secName, out var secData) && secData.Groups != null)
                {
                    foreach (var gKey in secData.Groups.Keys)
                    {
                        allGroups.Add(gKey);
                    }
                }
            }

            // Deep Sort across all datasets
            string GetMinAcc(string g)
            {
                string minAcc = "999999999";
                foreach (var ds in datasets)
                {
                    if (ds != null && ds.Structure != null && ds.Structure.TryGetValue(secName, out var secData) && secData.Groups != null)
                    {
                        if (secData.Groups.TryGetValue(g, out var grp) && grp.Items != null)
                        {
                            foreach (var item in grp.Items)
                            {
                                if (string.Compare(item.No, minAcc) < 0)
                                {
                                    minAcc = item.No;
                                }
                            }
                        }
                    }
                }
                return minAcc != "999999999" ? minAcc : g;
            }

            var sortedGroups = allGroups.OrderBy(GetMinAcc).ToList();

            foreach (var gName in sortedGroups)
            {
                Write(ws, r, 0, gName, GroupFmt);
                r++;

                var allItems = new Dictionary<string, string>();
                foreach (var ds in datasets)
                {
                    if (ds != null && ds.Structure != null && ds.Structure.TryGetValue(secName, out var secData) && secData.Groups != null)
                    {
                        if (secData.Groups.TryGetValue(gName, out var grp) && grp.Items != null)
                        {
                            foreach (var item in grp.Items)
                            {
                                allItems[item.No] = item.Name;
                            }
                        }
                    }
                }

                var sortedAccNos = allItems.Keys.OrderBy(x => x).ToList();

                foreach (var accNo in sortedAccNos)
                {
                    Write(ws, r, 0, $"{accNo} - {allItems[accNo]}", ItemFmt);
                    for (int colIdx = 0; colIdx < datasets.Count; colIdx++)
                    {
                        var ds = datasets[colIdx];
                        decimal val = 0;
                        if (ds != null && ds.Structure != null && ds.Structure.TryGetValue(secName, out var secData) && secData.Groups != null)
                        {
                            if (secData.Groups.TryGetValue(gName, out var grp) && grp.Items != null)
                            {
                                var matchedItem = grp.Items.FirstOrDefault(x => x.No == accNo);
                                if (matchedItem != null)
                                {
                                    val = matchedItem.Balance;
                                }
                            }
                        }
                        Write(ws, r, colIdx + 1, val, NumFmt);
                    }
                    r++;
                }

                Write(ws, r, 0, $"Total {gName}", GroupFmt);
                for (int colIdx = 0; colIdx < datasets.Count; colIdx++)
                {
                    var ds = datasets[colIdx];
                    decimal grpTotal = 0;
                    if (ds != null && ds.Structure != null && ds.Structure.TryGetValue(secName, out var secData) && secData.Groups != null)
                    {
                        if (secData.Groups.TryGetValue(gName, out var grp))
                        {
                            grpTotal = grp.Total;
                        }
                    }
                    Write(ws, r, colIdx + 1, grpTotal, GroupNumFmt);
                }
                r += 2;
            }

            // Section Totals
            var totals = new List<decimal>();
            for (int colIdx = 0; colIdx < datasets.Count; colIdx++)
            {
                var ds = datasets[colIdx];
                decimal t = 0;
                if (ds != null && ds.Structure != null && ds.Structure.TryGetValue(secName, out var secData))
                {
                    t = secData.Total;
                }
                totals.Add(t);
            }

            if (isEquity)
            {
                Write(ws, r, 0, "Current Year Earnings", ItemFmt);
                for (int colIdx = 0; colIdx < datasets.Count; colIdx++)
                {
                    var ds = datasets[colIdx];
                    decimal ni = ds != null ? ds.NetIncome : 0;
                    Write(ws, r, colIdx + 1, ni, NumFmt);
                    totals[colIdx] += ni;
                }
                r += 2;
            }

            Write(ws, r, 0, $"TOTAL {secName.ToUpper()}", SubtotalLblFmt);
            for (int colIdx = 0; colIdx < datasets.Count; colIdx++)
            {
                Write(ws, r, colIdx + 1, totals[colIdx], SubtotalNumFmt);
            }

            return (r + 2, totals);
        }

        // ==========================================
        // STYLING ACTIONS
        // ==========================================

        private static void TitleFmt(IXLStyle style)
        {
            style.Font.Bold = true;
            style.Font.FontSize = 16;
            style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static void SubtitleFmt(IXLStyle style)
        {
            style.Font.FontSize = 12;
            style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            style.Font.FontColor = XLColor.FromHtml("#666666");
        }

        private static void HeaderFmt(IXLStyle style)
        {
            style.Font.Bold = true;
            style.Border.BottomBorder = XLBorderStyleValues.Medium;
            style.Font.FontSize = 12;
            style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static void HeaderLblFmt(IXLStyle style)
        {
            style.Font.Bold = true;
            style.Border.BottomBorder = XLBorderStyleValues.Medium;
            style.Font.FontSize = 12;
        }

        private static void GroupFmt(IXLStyle style)
        {
            style.Font.Bold = true;
            style.Font.FontColor = XLColor.FromHtml("#0d6efd");
        }

        private static void GroupNumFmt(IXLStyle style)
        {
            style.Font.Bold = true;
            style.NumberFormat.Format = "#,##0.00;(#,##0.00)";
        }

        private static void ItemFmt(IXLStyle style)
        {
            style.Alignment.Indent = 1;
        }

        private static void NumFmt(IXLStyle style)
        {
            style.NumberFormat.Format = "#,##0.00;(#,##0.00)";
        }

        private static void TotalLblFmt(IXLStyle style)
        {
            style.Font.Bold = true;
            style.Border.TopBorder = XLBorderStyleValues.Thin;
            style.Border.BottomBorder = XLBorderStyleValues.Double;
        }

        private static void TotalNumFmt(IXLStyle style)
        {
            style.Font.Bold = true;
            style.Border.TopBorder = XLBorderStyleValues.Thin;
            style.Border.BottomBorder = XLBorderStyleValues.Double;
            style.NumberFormat.Format = "#,##0.00;(#,##0.00)";
        }

        private static void SubtotalLblFmt(IXLStyle style)
        {
            style.Font.Bold = true;
            style.Border.TopBorder = XLBorderStyleValues.Double;
            style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");
        }

        private static void SubtotalNumFmt(IXLStyle style)
        {
            style.Font.Bold = true;
            style.Border.TopBorder = XLBorderStyleValues.Double;
            style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");
            style.NumberFormat.Format = "#,##0.00;(#,##0.00)";
        }
    }
}
