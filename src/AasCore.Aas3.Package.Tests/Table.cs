using Math = System.Math;
using String = System.String;
using StringWriter = System.IO.StringWriter;

using System.Collections.Generic;  // can't alias
using System.Linq;  // can't alias

// This is necessary only for InspectCode.
// See https://resharper-support.jetbrains.com/hc/en-us/community/posts/360008362700-Not-recognising-Nullable-enable-Nullable-
#nullable enable

namespace AasCore.Aas3.Package.Tests
{
    /**
     * <summary>Draw a table as a string.</summary>
     * <remarks>
     * If you have new-line characters or tabs in your table, the appearance might be
     * scrambled.
     * </remarks>
     */
    public class Table
    {
        private readonly List<string> _headers;
        private readonly List<List<string>> _rows = new();

        public Table(List<string> headers)
        {
            _headers = headers;
        }

        /**
         * <summary>
         * Add the cells to the table.
         * </summary>
         * <remarks>
         * No cell may contain a new-line character or a tab.
         * If it does, the appearance of your table will be scrambled.
         * </remarks>
         */
        public void Add(IEnumerable<string> cells)
        {
            _rows.Add(cells.ToList());
        }

        /**
         * <summary>
         * Render the table as the ascii.
         *
         * If you did not specify the new-line character,
         * <see cref="System.Environment.NewLine"/> will be used.
         * </summary>
         */
        public string Render(string? newline = null)
        {
            newline ??= System.Environment.NewLine;

            List<int> columnWidths = _headers.Select(
                header => header.Length).ToList();

            int columnIndex;
            foreach (List<string> row in _rows)
            {
                columnIndex = 0;
                foreach (string cell in row)
                {
                    if (columnIndex + 1 > columnWidths.Count)
                    {
                        columnWidths.Add(cell.Length);
                    }
                    else
                    {
                        columnWidths[columnIndex] = Math.Max(columnWidths[columnIndex],
                            cell.Length);
                    }

                    columnIndex++;
                }
            }

            StringWriter writer = new();
            columnIndex = 0;
            foreach (string header in _headers)
            {
                if (columnIndex > 0)
                {
                    writer.Write(" | ");
                }

                writer.Write(columnIndex < _headers.Count - 1
                    ? header.PadRight(columnWidths[columnIndex])
                    : header);

                columnIndex++;
            }
            writer.Write(newline);

            columnIndex = 0;
            foreach (var _ in _headers)
            {
                if (columnIndex > 0)
                {
                    writer.Write("-+-");
                }

                writer.Write(new String('-', columnWidths[columnIndex]));

                columnIndex++;
            }

            foreach (List<string> row in _rows)
            {
                writer.Write(newline);

                columnIndex = 0;
                foreach (string cell in row)
                {
                    if (columnIndex > 0)
                    {
                        writer.Write(" | ");
                    }

                    writer.Write(columnIndex < row.Count - 1
                        ? cell.PadRight(columnWidths[columnIndex])
                        : cell);

                    columnIndex++;
                }
            }

            if (_rows.Count > 0)
            {
                writer.Write(newline);
            }

            return writer.ToString();
        }
    }
}