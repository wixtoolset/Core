// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.WindowsInstaller
{
    using System;
    using System.Collections.Generic;
    using WixToolset.Data.WindowsInstaller;

    /// <summary>
    /// A dictionary of rows. Unlike the RowIndexedList this
    /// will throw when multiple rows with the same key are added.
    /// </summary>
    internal sealed class RowDictionary<T> : Dictionary<string, T> where T : Row
    {
        /// <summary>
        /// Creates an empty <see cref="RowDictionary{T}"/>.
        /// </summary>
        public RowDictionary()
            : base(StringComparer.InvariantCulture)
        {
        }

        /// <summary>
        /// Creates and populates a <see cref="RowDictionary{T}"/> with the rows from the given <see cref="Table"/>.
        /// </summary>
        /// <param name="table">The table to index.</param>
        /// <remarks>
        /// Rows added to the index are not automatically added to the given <paramref name="table"/>.
        /// </remarks>
        public RowDictionary(Table table)
            : this()
        {
            if (null != table)
            {
                foreach (T row in table.Rows)
                {
                    this.Add(row);
                }
            }
        }

        /// <summary>
        /// Adds a row to the dictionary using the row key.
        /// </summary>
        /// <param name="row">Row to add to the dictionary.</param>
        public void Add(T row)
        {
            this.Add(row.GetKey(), row);
        }

        /// <summary>
        /// Gets the row by integer key.
        /// </summary>
        /// <param name="key">Integer key to look up.</param>
        /// <returns>Row or null if key is not found.</returns>
        public T Get(int key)
        {
            return this.Get(key.ToString());
        }

        /// <summary>
        /// Gets the row by string key.
        /// </summary>
        /// <param name="key">String key to look up.</param>
        /// <returns>Row or null if key is not found.</returns>
        public T Get(string key)
        {
            return this.TryGetValue(key, out var result) ? result : null;
        }
    }
}
