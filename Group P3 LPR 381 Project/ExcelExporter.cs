using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Group_P3_LPR_381_Project.IO
{
    public static class ExcelExporter
    {
        public static void ExportSimplexResults(string filePath, List<double[,]> tableaus, double[] solution)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Simplex");

            int row = 1;
            foreach (var tableau in tableaus)
            {
                sheet.Cells[row++, 1].Value = "Tableau";
                for (int i = 0; i < tableau.GetLength(0); i++)
                    for (int j = 0; j < tableau.GetLength(1); j++)
                        sheet.Cells[row + i, j + 1].Value = Math.Round(tableau[i, j], 3);
                row += tableau.GetLength(0) + 2;
            }

            sheet.Cells[row++, 1].Value = "Solution";
            for (int i = 0; i < solution.Length; i++)
                sheet.Cells[row, i + 1].Value = Math.Round(solution[i], 3);

            package.SaveAs(new FileInfo(filePath));
        }
    }
}
