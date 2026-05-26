using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BimasaktiReports.FinancialReports.Backend.Engines
{
    public static class prFinancialPDF
    {
        private static readonly XColor PrimaryColor = XColor.FromArgb(0, 0, 0);         // Black
        private static readonly XColor SecondaryColor = XColor.FromArgb(227, 239, 255); // Soft Sky Blue (#e3efff)
        private static readonly XColor AccentColor = XColor.FromArgb(0, 0, 0);          // Black
        private static readonly XColor LightBackground = XColor.FromArgb(235, 243, 253); // Soft XP Pastel Blue (#ebf3fd)
        private static readonly XColor BorderColor = XColor.FromArgb(149, 191, 233);     // XP Border Blue (#95bfe9)

        private static readonly XFont TitleFont = new("Arial", 16, XFontStyle.Bold);
        private static readonly XFont SubtitleFont = new("Arial", 10, XFontStyle.Regular);
        private static readonly XFont HeaderFont = new("Arial", 11, XFontStyle.Bold);
        private static readonly XFont SectionFont = new("Arial", 10, XFontStyle.Bold);
        private static readonly XFont GroupFont = new("Arial", 9, XFontStyle.Bold);
        private static readonly XFont BodyFont = new("Arial", 9, XFontStyle.Regular);
        private static readonly XFont TotalFont = new("Arial", 9, XFontStyle.Bold);

        private class PdfContext
        {
            public PdfDocument Document { get; set; } = null!;
            public PdfPage CurrentPage { get; set; } = null!;
            public XGraphics Graphics { get; set; } = null!;
            public double CurrentY { get; set; }
            public int PageNumber { get; set; }
            public double Margin { get; set; } = 40;
            public double PageWidth { get; set; }
            public double PageHeight { get; set; }
            public string Company { get; set; } = "";
            public string PeriodInfo { get; set; } = "";
            public bool IsDetailComparison { get; set; }
            public string ColumnHeader1 { get; set; } = "";
            public string ColumnHeader2 { get; set; } = "";
            public string ColumnHeader3 { get; set; } = "";
        }

        public static MemoryStream CreatePdfReport(ExcelReportPayload payload)
        {
            using var document = new PdfDocument();
            document.Info.Title = $"Financial Report - {payload.Company}";
            document.Info.Author = "Bimasakti Financial Reports";

            var context = new PdfContext
            {
                Document = document,
                Company = payload.Company,
                PeriodInfo = $"{payload.PeriodName} {payload.Year}"
            };

            // --- PAGE 1: Balance Sheet ---
            AddNewPage(context, "BALANCE SHEET SUMMARY");
            DrawBalanceSheet(context, payload);

            // --- PAGE 2: Income Statement ---
            AddNewPage(context, "INCOME STATEMENT SUMMARY");
            DrawIncomeStatement(context, payload);

            // --- PAGE 3: Balance Sheet Detail Comparison ---
            AddNewPage(context, "BALANCE SHEET - DETAIL COMPARISON");
            DrawMultiCompSection(context, "Assets", payload.CurrData, payload.PrevMoData, payload.PrevYrData, payload);
            DrawMultiCompSection(context, "Liabilities", payload.CurrData, payload.PrevMoData, payload.PrevYrData, payload);
            DrawMultiCompSection(context, "Equity", payload.CurrData, payload.PrevMoData, payload.PrevYrData, payload, isEquity: true);

            // --- PAGE 4: Income Statement Detail Comparison ---
            AddNewPage(context, "INCOME STATEMENT - DETAIL COMPARISON");
            DrawMultiCompSection(context, "Revenue", payload.CurrData, payload.PrevMoData, payload.PrevYrData, payload);
            DrawMultiCompSection(context, "Expenses", payload.CurrData, payload.PrevMoData, payload.PrevYrData, payload, isNetIncome: true);

            var stream = new MemoryStream();
            document.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private static void AddNewPage(PdfContext context, string reportTitle)
        {
            context.CurrentPage = context.Document.AddPage();
            context.CurrentPage.Size = PageSize.A4;
            context.PageWidth = context.CurrentPage.Width.Point;
            context.PageHeight = context.CurrentPage.Height.Point;
            context.Graphics = XGraphics.FromPdfPage(context.CurrentPage);
            context.PageNumber++;
            context.CurrentY = context.Margin;

            // Draw header background strip with neutral background
            context.Graphics.DrawRectangle(XBrushes.White, 0, 0, context.PageWidth, 60);
            context.Graphics.DrawLine(new XPen(BorderColor, 1), 0, 60, context.PageWidth, 60);

            string logoPath = GetLogoPath(context.Company);
            bool hasLogo = File.Exists(logoPath);
            double textStartX = context.Margin + (hasLogo ? 50 : 0);

            if (hasLogo)
            {
                try
                {
                    // Draw a rounded card with a soft neutral background and border for padding the logo
                    context.Graphics.DrawRoundedRectangle(
                        new XPen(BorderColor, 1),
                        new XSolidBrush(LightBackground),
                        context.Margin - 5,
                        5,
                        50,
                        50,
                        8,
                        8
                    );
                    using (var image = XImage.FromFile(logoPath))
                    {
                        context.Graphics.DrawImage(image, context.Margin, 10, 40, 40);
                    }
                }
                catch
                {
                    textStartX = context.Margin;
                }
            }

            // Draw header text in black / dark slate
            context.Graphics.DrawString(context.Company.ToUpper(), TitleFont, XBrushes.Black, new XPoint(textStartX, 15), XStringFormats.TopLeft);
            context.Graphics.DrawString($"{reportTitle} - {context.PeriodInfo}", SubtitleFont, new XSolidBrush(XColor.FromArgb(100, 116, 139)), new XPoint(textStartX, 38), XStringFormats.TopLeft);

            // Draw footer strip
            context.Graphics.DrawLine(new XPen(BorderColor, 1), context.Margin, context.PageHeight - 35, context.PageWidth - context.Margin, context.PageHeight - 35);
            context.Graphics.DrawString($"Page {context.PageNumber}", BodyFont, XBrushes.Gray, new XPoint(context.PageWidth - context.Margin, context.PageHeight - 30), XStringFormats.TopRight);
            context.Graphics.DrawString("Bimasakti Financial Reports Manager", BodyFont, XBrushes.Gray, new XPoint(context.Margin, context.PageHeight - 30), XStringFormats.TopLeft);

            context.CurrentY = 85;

            if (context.IsDetailComparison)
            {
                DrawTableHeaders(context);
                context.CurrentY += 5; // Extra padding below headers
            }
        }

        private static void DrawTableHeaders(PdfContext context)
        {
            double tableWidth = context.PageWidth - (context.Margin * 2);
            double valueColumnWidth = 85;

            context.Graphics.DrawRectangle(new XSolidBrush(LightBackground), context.Margin, context.CurrentY, tableWidth, 20);
            context.Graphics.DrawString("ACCOUNT", GroupFont, new XSolidBrush(PrimaryColor), new XPoint(context.Margin + 5, context.CurrentY + 4), XStringFormats.TopLeft);

            double position1 = context.PageWidth - context.Margin - 5 - 2 * valueColumnWidth;
            double position2 = context.PageWidth - context.Margin - 5 - valueColumnWidth;
            double position3 = context.PageWidth - context.Margin - 5;

            // Defensive header truncation to prevent overlapping
            string header1Text = TruncateString(context.Graphics, context.ColumnHeader1, GroupFont, valueColumnWidth - 5);
            string header2Text = TruncateString(context.Graphics, context.ColumnHeader2, GroupFont, valueColumnWidth - 5);
            string header3Text = TruncateString(context.Graphics, context.ColumnHeader3, GroupFont, valueColumnWidth - 5);

            context.Graphics.DrawString(header1Text, GroupFont, new XSolidBrush(PrimaryColor), new XPoint(position1, context.CurrentY + 4), XStringFormats.TopRight);
            context.Graphics.DrawString(header2Text, GroupFont, new XSolidBrush(PrimaryColor), new XPoint(position2, context.CurrentY + 4), XStringFormats.TopRight);
            context.Graphics.DrawString(header3Text, GroupFont, new XSolidBrush(PrimaryColor), new XPoint(position3, context.CurrentY + 4), XStringFormats.TopRight);

            context.Graphics.DrawLine(new XPen(BorderColor, 1), context.Margin, context.CurrentY + 20, context.PageWidth - context.Margin, context.CurrentY + 20);
            context.CurrentY += 20;
        }

        private static void EnsureSpace(PdfContext context, double height, string reportTitle)
        {
            if (context.CurrentY + height > context.PageHeight - 50)
            {
                AddNewPage(context, reportTitle);
            }
        }

        private static string FormatValue(decimal value)
        {
            return value.ToString("#,##0.00;(#,##0.00);0.00");
        }

        private static string TruncateString(XGraphics graphics, string text, XFont font, double maxWidth)
        {
            if (graphics.MeasureString(text, font).Width <= maxWidth)
            {
                return text;
            }

            string ellipsis = "...";
            int length = text.Length;
            while (length > 0)
            {
                string substring = string.Concat(text.AsSpan(0, length), ellipsis);
                if (graphics.MeasureString(substring, font).Width <= maxWidth)
                {
                    return substring;
                }
                length--;
            }
            return ellipsis;
        }

        private static void DrawBalanceSheet(PdfContext context, ExcelReportPayload payload)
        {
            var currentData = payload.CurrData;
            Dictionary<string, LedgerSectionData> structure = currentData?.Structure ?? new();

            structure.TryGetValue("Assets", out var assetsSection);
            structure.TryGetValue("Liabilities", out var liabilitiesSection);
            structure.TryGetValue("Equity", out var equitySection);

            // Vertical stacked Layout for Portrait Page
            DrawStandardSection(context, "ASSETS", assetsSection, "BALANCE SHEET SUMMARY");
            DrawStandardSection(context, "LIABILITIES", liabilitiesSection, "BALANCE SHEET SUMMARY");
            DrawStandardSection(context, "EQUITY", equitySection, "BALANCE SHEET SUMMARY", includeEarnings: true, netIncome: currentData?.NetIncome ?? 0);

            // Draw Final Totals
            EnsureSpace(context, 35, "BALANCE SHEET SUMMARY");
            double tableWidth = context.PageWidth - (context.Margin * 2);

            // Total Assets
            context.Graphics.DrawRectangle(new XSolidBrush(LightBackground), context.Margin, context.CurrentY, tableWidth, 22);
            context.Graphics.DrawString("TOTAL ASSETS", TotalFont, new XSolidBrush(PrimaryColor), new XPoint(context.Margin + 10, context.CurrentY + 6), XStringFormats.TopLeft);
            context.Graphics.DrawString(FormatValue(assetsSection?.Total ?? 0), TotalFont, new XSolidBrush(PrimaryColor), new XPoint(context.PageWidth - context.Margin - 10, context.CurrentY + 6), XStringFormats.TopRight);
            context.Graphics.DrawLine(new XPen(PrimaryColor, 1.5), context.Margin, context.CurrentY + 22, context.PageWidth - context.Margin, context.CurrentY + 22);
            context.CurrentY += 30;

            // Total Liabilities & Equity
            decimal totalLiabilitiesEquity = (liabilitiesSection?.Total ?? 0) + (equitySection?.Total ?? 0) + (currentData?.NetIncome ?? 0);
            EnsureSpace(context, 25, "BALANCE SHEET SUMMARY");
            context.Graphics.DrawRectangle(new XSolidBrush(LightBackground), context.Margin, context.CurrentY, tableWidth, 22);
            context.Graphics.DrawString("TOTAL LIABILITIES & EQUITY", TotalFont, new XSolidBrush(PrimaryColor), new XPoint(context.Margin + 10, context.CurrentY + 6), XStringFormats.TopLeft);
            context.Graphics.DrawString(FormatValue(totalLiabilitiesEquity), TotalFont, new XSolidBrush(PrimaryColor), new XPoint(context.PageWidth - context.Margin - 10, context.CurrentY + 6), XStringFormats.TopRight);
            context.Graphics.DrawLine(new XPen(PrimaryColor, 1.5), context.Margin, context.CurrentY + 22, context.PageWidth - context.Margin, context.CurrentY + 22);
            context.CurrentY += 25;
        }

        private static void DrawIncomeStatement(PdfContext context, ExcelReportPayload payload)
        {
            var currentData = payload.CurrData;
            Dictionary<string, LedgerSectionData> structure = currentData?.Structure ?? new();

            structure.TryGetValue("Revenue", out var revenueSection);
            structure.TryGetValue("Expenses", out var expensesSection);

            DrawStandardSection(context, "REVENUE", revenueSection, "INCOME STATEMENT SUMMARY");
            DrawStandardSection(context, "EXPENSES", expensesSection, "INCOME STATEMENT SUMMARY");

            // Net Income Total Block
            EnsureSpace(context, 35, "INCOME STATEMENT SUMMARY");
            double tableWidth = context.PageWidth - (context.Margin * 2);

            context.Graphics.DrawRectangle(new XSolidBrush(LightBackground), context.Margin, context.CurrentY, tableWidth, 24);
            context.Graphics.DrawString("NET INCOME / (LOSS)", TotalFont, new XSolidBrush(AccentColor), new XPoint(context.Margin + 10, context.CurrentY + 7), XStringFormats.TopLeft);
            context.Graphics.DrawString(FormatValue(currentData?.NetIncome ?? 0), TotalFont, new XSolidBrush(AccentColor), new XPoint(context.PageWidth - context.Margin - 10, context.CurrentY + 7), XStringFormats.TopRight);
            context.Graphics.DrawLine(new XPen(AccentColor, 2), context.Margin, context.CurrentY + 24, context.PageWidth - context.Margin, context.CurrentY + 24);
            context.CurrentY += 30;
        }

        private static void DrawStandardSection(
            PdfContext context,
            string sectionName,
            LedgerSectionData? data,
            string reportTitle,
            bool includeEarnings = false,
            decimal netIncome = 0)
        {
            EnsureSpace(context, 50, reportTitle);

            double tableWidth = context.PageWidth - (context.Margin * 2);

            // Draw Section Header
            context.Graphics.DrawRectangle(new XSolidBrush(PrimaryColor), context.Margin, context.CurrentY, tableWidth, 22);
            context.Graphics.DrawString(sectionName, HeaderFont, XBrushes.White, new XPoint(context.Margin + 10, context.CurrentY + 5), XStringFormats.TopLeft);
            context.Graphics.DrawString("BALANCE", HeaderFont, XBrushes.White, new XPoint(context.PageWidth - context.Margin - 10, context.CurrentY + 5), XStringFormats.TopRight);
            context.CurrentY += 22;

            if (data == null || data.Groups == null || data.Groups.Count == 0)
            {
                context.Graphics.DrawString("No data available", BodyFont, XBrushes.Gray, new XPoint(context.Margin + 10, context.CurrentY + 5), XStringFormats.TopLeft);
                context.CurrentY += 25;
                return;
            }

            // Sort Groups by minimum account number
            var groups = data.Groups;
            string GetMinimumAccount(string groupName)
            {
                string minimumAccount = "999999999";
                if (groups.TryGetValue(groupName, out var groupData) && groupData.Items != null)
                {
                    foreach (var item in groupData.Items)
                    {
                        if (string.Compare(item.No, minimumAccount) < 0) minimumAccount = item.No;
                    }
                }
                return minimumAccount != "999999999" ? minimumAccount : groupName;
            }
            var sortedGroupNames = groups.Keys.OrderBy(GetMinimumAccount).ToList();

            foreach (var groupName in sortedGroupNames)
            {
                var group = groups[groupName];
                EnsureSpace(context, 20, reportTitle);

                // Alternating row styling or border
                context.Graphics.DrawLine(new XPen(BorderColor, 1), context.Margin, context.CurrentY + 18, context.PageWidth - context.Margin, context.CurrentY + 18);
                // Defensive group name truncation to avoid value overlaps
                string displayGroupName = TruncateString(context.Graphics, groupName, SectionFont, tableWidth - 130);
                context.Graphics.DrawString(displayGroupName, SectionFont, new XSolidBrush(PrimaryColor), new XPoint(context.Margin + 10, context.CurrentY + 4), XStringFormats.TopLeft);
                context.Graphics.DrawString(FormatValue(group.Total), TotalFont, new XSolidBrush(PrimaryColor), new XPoint(context.PageWidth - context.Margin - 10, context.CurrentY + 4), XStringFormats.TopRight);
                context.CurrentY += 18;
            }

            if (includeEarnings)
            {
                EnsureSpace(context, 20, reportTitle);
                context.Graphics.DrawLine(new XPen(BorderColor, 1), context.Margin, context.CurrentY + 18, context.PageWidth - context.Margin, context.CurrentY + 18);
                context.Graphics.DrawString("Current Year Earnings", SectionFont, new XSolidBrush(PrimaryColor), new XPoint(context.Margin + 10, context.CurrentY + 4), XStringFormats.TopLeft);
                context.Graphics.DrawString(FormatValue(netIncome), TotalFont, new XSolidBrush(PrimaryColor), new XPoint(context.PageWidth - context.Margin - 10, context.CurrentY + 4), XStringFormats.TopRight);
                context.CurrentY += 18;
            }

            // Draw Section Total
            decimal finalTotal = data.Total + (includeEarnings ? netIncome : 0);
            EnsureSpace(context, 25, reportTitle);
            context.Graphics.DrawRectangle(new XSolidBrush(LightBackground), context.Margin, context.CurrentY, tableWidth, 20);
            context.Graphics.DrawString($"TOTAL {sectionName}", TotalFont, new XSolidBrush(AccentColor), new XPoint(context.Margin + 10, context.CurrentY + 5), XStringFormats.TopLeft);
            context.Graphics.DrawString(FormatValue(finalTotal), TotalFont, new XSolidBrush(AccentColor), new XPoint(context.PageWidth - context.Margin - 10, context.CurrentY + 5), XStringFormats.TopRight);
            context.Graphics.DrawLine(new XPen(AccentColor, 1.2), context.Margin, context.CurrentY + 20, context.PageWidth - context.Margin, context.CurrentY + 20);
            context.CurrentY += 28;
        }

        private static void DrawMultiCompSection(
            PdfContext context,
            string sectionName,
            LedgerReportData? current,
            LedgerReportData? previousMonth,
            LedgerReportData? previousYear,
            ExcelReportPayload payload,
            bool isEquity = false,
            bool isNetIncome = false)
        {
            string reportTitle = isNetIncome ? "INCOME STATEMENT - DETAIL COMPARISON" : "BALANCE SHEET - DETAIL COMPARISON";
            EnsureSpace(context, 60, reportTitle);

            context.IsDetailComparison = true;
            context.ColumnHeader1 = $"{payload.PeriodName} {payload.Year}";
            context.ColumnHeader2 = $"{payload.LastMonthName} {payload.LastMonthYear}".Trim();
            context.ColumnHeader3 = $"{payload.LastYearName} {payload.LastYearYear}".Trim();

            double tableWidth = context.PageWidth - (context.Margin * 2);
            double valueColumnWidth = 85;

            // Draw Category Header Bar
            context.Graphics.DrawRectangle(new XSolidBrush(PrimaryColor), context.Margin, context.CurrentY, tableWidth, 22);
            context.Graphics.DrawString(sectionName.ToUpper(), HeaderFont, XBrushes.White, new XPoint(context.Margin + 10, context.CurrentY + 5), XStringFormats.TopLeft);
            context.CurrentY += 22;

            // Table Columns Headers
            DrawTableHeaders(context);

            var datasets = new List<LedgerReportData?> { current, previousMonth, previousYear };

            var allGroups = new HashSet<string>();
            foreach (var dataset in datasets)
            {
                if (dataset != null && dataset.Structure != null && dataset.Structure.TryGetValue(sectionName, out var sectionData) && sectionData.Groups != null)
                {
                    foreach (var groupKey in sectionData.Groups.Keys)
                    {
                        allGroups.Add(groupKey);
                    }
                }
            }

            string GetMinimumAccount(string groupName)
            {
                string minimumAccount = "999999999";
                foreach (var dataset in datasets)
                {
                    if (dataset != null && dataset.Structure != null && dataset.Structure.TryGetValue(sectionName, out var sectionData) && sectionData.Groups != null)
                    {
                        if (sectionData.Groups.TryGetValue(groupName, out var group) && group.Items != null)
                        {
                            foreach (var item in group.Items)
                            {
                                if (string.Compare(item.No, minimumAccount) < 0)
                                {
                                    minimumAccount = item.No;
                                }
                            }
                        }
                    }
                }
                return minimumAccount != "999999999" ? minimumAccount : groupName;
            }
            var sortedGroups = allGroups.OrderBy(GetMinimumAccount).ToList();

            foreach (var groupName in sortedGroups)
            {
                EnsureSpace(context, 22, reportTitle);
                // Draw Group Title (defensively truncated at tableWidth - 10 to fit page)
                string displayGroupName = TruncateString(context.Graphics, groupName, GroupFont, tableWidth - 10);
                context.Graphics.DrawString(displayGroupName, GroupFont, new XSolidBrush(AccentColor), new XPoint(context.Margin + 5, context.CurrentY + 4), XStringFormats.TopLeft);
                context.Graphics.DrawLine(new XPen(BorderColor, 0.8), context.Margin, context.CurrentY + 18, context.PageWidth - context.Margin, context.CurrentY + 18);
                context.CurrentY += 18;

                var allItems = new Dictionary<string, string>();
                foreach (var dataset in datasets)
                {
                    if (dataset != null && dataset.Structure != null && dataset.Structure.TryGetValue(sectionName, out var sectionData) && sectionData.Groups != null)
                    {
                        if (sectionData.Groups.TryGetValue(groupName, out var group) && group.Items != null)
                        {
                            foreach (var item in group.Items)
                            {
                                allItems[item.No] = item.Name;
                            }
                        }
                    }
                }
                var sortedAccountNumbers = allItems.Keys.OrderBy(number => number).ToList();

                foreach (var accountNumber in sortedAccountNumbers)
                {
                    EnsureSpace(context, 16, reportTitle);

                    // Perfectly calculated space to prevent overlapping Column 0 (which starts at context.PageWidth - context.Margin - 5 - 3 * valueColumnWidth)
                    double maxAccountNameWidth = (context.PageWidth - context.Margin - 5 - 3 * valueColumnWidth) - (context.Margin + 15) - 15;
                    string accountText = $"{accountNumber} - {allItems[accountNumber]}";
                    string displayAccountText = TruncateString(context.Graphics, accountText, BodyFont, maxAccountNameWidth);

                    context.Graphics.DrawString(displayAccountText, BodyFont, new XSolidBrush(PrimaryColor), new XPoint(context.Margin + 15, context.CurrentY + 3), XStringFormats.TopLeft);

                    for (int datasetIndex = 0; datasetIndex < datasets.Count; datasetIndex++)
                    {
                        var dataset = datasets[datasetIndex];
                        decimal value = 0;
                        if (dataset != null && dataset.Structure != null && dataset.Structure.TryGetValue(sectionName, out var sectionData) && sectionData.Groups != null)
                        {
                            if (sectionData.Groups.TryGetValue(groupName, out var group) && group.Items != null)
                            {
                                var matched = group.Items.FirstOrDefault(item => item.No == accountNumber);
                                if (matched != null)
                                {
                                    value = matched.Balance;
                                }
                            }
                        }

                        double xPosition = context.PageWidth - context.Margin - 5 - (2 - datasetIndex) * valueColumnWidth;
                        context.Graphics.DrawString(FormatValue(value), BodyFont, new XSolidBrush(PrimaryColor), new XPoint(xPosition, context.CurrentY + 3), XStringFormats.TopRight);
                    }
                    context.Graphics.DrawLine(new XPen(BorderColor, 0.5), context.Margin, context.CurrentY + 15, context.PageWidth - context.Margin, context.CurrentY + 15);
                    context.CurrentY += 15;
                }

                // Group Total Row
                EnsureSpace(context, 18, reportTitle);
                // Defensively truncated Group Total label to prevent value overlaps
                string groupTotalText = $"Total {groupName}";
                double maxGroupTotalWidth = (context.PageWidth - context.Margin - 5 - 3 * valueColumnWidth) - (context.Margin + 5) - 15;
                string displayGroupTotal = TruncateString(context.Graphics, groupTotalText, GroupFont, maxGroupTotalWidth);
                context.Graphics.DrawString(displayGroupTotal, GroupFont, new XSolidBrush(PrimaryColor), new XPoint(context.Margin + 5, context.CurrentY + 4), XStringFormats.TopLeft);

                for (int datasetIndex = 0; datasetIndex < datasets.Count; datasetIndex++)
                {
                    var dataset = datasets[datasetIndex];
                    decimal groupTotal = 0;
                    if (dataset != null && dataset.Structure != null && dataset.Structure.TryGetValue(sectionName, out var sectionData) && sectionData.Groups != null)
                    {
                        if (sectionData.Groups.TryGetValue(groupName, out var group))
                        {
                            groupTotal = group.Total;
                        }
                    }

                    double xPosition = context.PageWidth - context.Margin - 5 - (2 - datasetIndex) * valueColumnWidth;
                    context.Graphics.DrawString(FormatValue(groupTotal), GroupFont, new XSolidBrush(PrimaryColor), new XPoint(xPosition, context.CurrentY + 4), XStringFormats.TopRight);
                }
                context.Graphics.DrawLine(new XPen(PrimaryColor, 0.8), context.Margin, context.CurrentY + 16, context.PageWidth - context.Margin, context.CurrentY + 16);
                context.CurrentY += 22;
            }

            // Section Totals
            var totals = new List<decimal>();
            for (int datasetIndex = 0; datasetIndex < datasets.Count; datasetIndex++)
            {
                var dataset = datasets[datasetIndex];
                decimal total = 0;
                if (dataset != null && dataset.Structure != null && dataset.Structure.TryGetValue(sectionName, out var sectionData))
                {
                    total = sectionData.Total;
                }
                totals.Add(total);
            }

            if (isEquity)
            {
                EnsureSpace(context, 18, reportTitle);
                context.Graphics.DrawString("Current Year Earnings", BodyFont, new XSolidBrush(PrimaryColor), new XPoint(context.Margin + 15, context.CurrentY + 3), XStringFormats.TopLeft);
                for (int datasetIndex = 0; datasetIndex < datasets.Count; datasetIndex++)
                {
                    var dataset = datasets[datasetIndex];
                    decimal netIncomeValue = dataset != null ? dataset.NetIncome : 0;
                    double xPosition = context.PageWidth - context.Margin - 5 - (2 - datasetIndex) * valueColumnWidth;
                    context.Graphics.DrawString(FormatValue(netIncomeValue), BodyFont, new XSolidBrush(PrimaryColor), new XPoint(xPosition, context.CurrentY + 3), XStringFormats.TopRight);
                    totals[datasetIndex] += netIncomeValue;
                }
                context.Graphics.DrawLine(new XPen(BorderColor, 0.5), context.Margin, context.CurrentY + 16, context.PageWidth - context.Margin, context.CurrentY + 16);
                context.CurrentY += 18;
            }

            // Section Total Row
            EnsureSpace(context, 22, reportTitle);
            context.Graphics.DrawRectangle(new XSolidBrush(LightBackground), context.Margin, context.CurrentY, tableWidth, 20);

            // Defensively truncated Section Total label to prevent value overlaps
            string sectionTotalText = $"TOTAL {sectionName.ToUpper()}";
            double maxSectionTotalWidth = (context.PageWidth - context.Margin - 5 - 3 * valueColumnWidth) - (context.Margin + 5) - 15;
            string displaySectionTotal = TruncateString(context.Graphics, sectionTotalText, TotalFont, maxSectionTotalWidth);
            context.Graphics.DrawString(displaySectionTotal, TotalFont, new XSolidBrush(AccentColor), new XPoint(context.Margin + 5, context.CurrentY + 5), XStringFormats.TopLeft);

            for (int datasetIndex = 0; datasetIndex < datasets.Count; datasetIndex++)
            {
                double xPosition = context.PageWidth - context.Margin - 5 - (2 - datasetIndex) * valueColumnWidth;
                context.Graphics.DrawString(FormatValue(totals[datasetIndex]), TotalFont, new XSolidBrush(AccentColor), new XPoint(xPosition, context.CurrentY + 5), XStringFormats.TopRight);
            }
            context.Graphics.DrawLine(new XPen(AccentColor, 1.2), context.Margin, context.CurrentY + 20, context.PageWidth - context.Margin, context.CurrentY + 20);
            context.CurrentY += 30;

            if (isNetIncome)
            {
                // Net Income row for MoM IS
                EnsureSpace(context, 25, reportTitle);
                context.Graphics.DrawRectangle(new XSolidBrush(LightBackground), context.Margin, context.CurrentY, tableWidth, 22);
                context.Graphics.DrawString("NET INCOME / (LOSS)", TotalFont, new XSolidBrush(AccentColor), new XPoint(context.Margin + 5, context.CurrentY + 6), XStringFormats.TopLeft);
                for (int datasetIndex = 0; datasetIndex < datasets.Count; datasetIndex++)
                {
                    var dataset = datasets[datasetIndex];
                    double xPosition = context.PageWidth - context.Margin - 5 - (2 - datasetIndex) * valueColumnWidth;
                    context.Graphics.DrawString(FormatValue(dataset?.NetIncome ?? 0), TotalFont, new XSolidBrush(AccentColor), new XPoint(xPosition, context.CurrentY + 6), XStringFormats.TopRight);
                }
                context.Graphics.DrawLine(new XPen(AccentColor, 2), context.Margin, context.CurrentY + 22, context.PageWidth - context.Margin, context.CurrentY + 22);
                context.CurrentY += 28;
            }

            context.IsDetailComparison = false;
        }

        private static string GetCompanyId(string companyName)
        {
            if (string.IsNullOrEmpty(companyName)) return "BMS";
            string lower = companyName.ToLowerInvariant();
            if (lower.Contains("agung") || lower.Contains("sedayu") || lower.Contains("residences") || lower.Contains("ashmd"))
                return "ASHMD";
            if (lower.Contains("grand") || lower.Contains("leasing") || lower.Contains("mall") || lower.Contains("pcgr1"))
                return "PCGR1";
            return "BMS";
        }

        private static string GetLogoPath(string companyName)
        {
            string safeId = GetCompanyId(companyName);
            string baseDir = AppContext.BaseDirectory;
            string? backendDir = null;
            string current = baseDir;
            while (!string.IsNullOrEmpty(current))
            {
                string dirName = Path.GetFileName(current);
                if (dirName.Equals("backend", StringComparison.OrdinalIgnoreCase))
                {
                    backendDir = current;
                    break;
                }
                if (File.Exists(Path.Combine(current, "BimasaktiReports.slnx")))
                {
                    backendDir = Path.Combine(current, "backend");
                    break;
                }
                string? parent = Path.GetDirectoryName(current);
                if (parent == current || string.IsNullOrEmpty(parent)) break;
                current = parent;
            }

            if (backendDir == null)
            {
                current = Directory.GetCurrentDirectory();
                while (!string.IsNullOrEmpty(current))
                {
                    string dirName = Path.GetFileName(current);
                    if (dirName.Equals("backend", StringComparison.OrdinalIgnoreCase))
                    {
                        backendDir = current;
                        break;
                    }
                    if (File.Exists(Path.Combine(current, "BimasaktiReports.slnx")))
                    {
                        backendDir = Path.Combine(current, "backend");
                        break;
                    }
                    string? parent = Path.GetDirectoryName(current);
                    if (parent == current || string.IsNullOrEmpty(parent)) break;
                    current = parent;
                }
            }

            if (backendDir == null)
            {
                backendDir = baseDir;
            }

            return Path.Combine(backendDir, "assets", safeId, "img", $"{safeId}_logo.png");
        }
    }
}
