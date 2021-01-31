using Npgsql;
using System;
using System.Collections.Generic;

namespace Honjo.Framework.Database
{
    /// <summary>
    /// A data, retrieved using a sql query. The connection is still open, and only closed using <see cref="Dispose"/> or <see cref="Unpack"/>/<see cref="UnpackRaw"/>.
    /// Use it with a using clause or remember to dispose it.
    /// </summary>
    public class SqlData : IDisposable
    {
        private NpgsqlCommand __underlyingCmd;
        /// <summary>
        /// Underlying npgsql connection
        /// </summary>
        protected NpgsqlConnection _conn;
        
        /// <summary>
        /// The native sql reader
        /// </summary>
        public NpgsqlDataReader SqlReader { get; protected set; }
        /// <summary>
        /// Is there data to read ? Does NOT say if it has MORE data, only if it ever was
        /// </summary>
        public virtual bool HasData => SqlReader.HasRows;

        /// <summary>
        /// Creates a new sql data
        /// </summary>
        /// <param name="connection">The npgsql connection</param>
        /// <param name="reader">The npgsql reader retrieved with the query</param>
        /// <param name="underlying">The underlying command (used to dispose it)</param>
        public SqlData(NpgsqlConnection connection, NpgsqlDataReader reader, NpgsqlCommand underlying)
        {
            SqlReader = reader;
            _conn = connection;
            __underlyingCmd = underlying;
        }

        /// <summary>
        /// Goes to the next row
        /// </summary>
        /// <returns>Whether there is more data to read</returns>
        public virtual bool NextRow() => SqlReader.Read();

        /// <summary>
        /// Gets the content of a column
        /// </summary>
        /// <typeparam name="T">Expected type</typeparam>
        /// <param name="column">Column index</param>
        /// <returns>The content of the column at given column index in the current row</returns>
        public virtual T Get<T>(int column) => (T) SqlReader[column];

        /// <summary>
        /// Gets the content of a column
        /// </summary>
        /// <typeparam name="T">Expected type</typeparam>
        /// <param name="column">Column name</param>
        /// <returns>The content of the column with the given column name in the current row</returns>
        public virtual T Get<T>(string column) => (T) SqlReader[column];

        /// <summary>
        /// Unpacks the retrieved data. Once unpacked, the connection is closed.
        /// </summary>
        /// <returns>A 2d array of which the first index is the row and the 2nd is the column to get the content of</returns>
        /// <remarks>If data has previously been read normally, it will not be unpacked</remarks>
        public virtual object[,] UnpackRaw()
        {
            var rows = new List<object[]>();
            object[,] dout;
            using (this)
            {
                while(NextRow())
                {
                    object[] temp = new object[SqlReader.FieldCount];
                    for (int i = 0; i < temp.Length; i++)
                        temp[i] = SqlReader[i];
                    rows.Add(temp);
                }

                if (rows.Count == 0)
                    return new object[0, 0];

                dout = new object[rows.Count, rows[0].Length];
                for (int i = 0; i < rows.Count; i++)
                {
                    object[] array = rows[i];
                    for (int j = 0; j < array.Length; j++)
                    {
                        dout[i, j] = array[j];
                    }
                }
            }

            return dout;
        }

        /// <summary>
        /// Unpacks the retrieved data. Once unpacked the connection is closed.
        /// </summary>
        /// <returns>A 2d array of which the index is the row and the content is then obtained through the column name</returns>
        /// <remarks>If data has previously been read normally, it will not be unpacked</remarks>
        public virtual Dictionary<string, object>[] Unpack()
        {
            var rows = new List<Dictionary<string, object>>();
            using (this)
            {
                while(NextRow())
                {
                    Dictionary<string, object> row = new Dictionary<string, object>();
                    for (int i = 0; i < SqlReader.FieldCount; i++)
                        row.Add(SqlReader.GetName(i), SqlReader[i]);

                    rows.Add(row);
                }
            }

            return rows.ToArray();
        }

        /// <summary>
        /// Disposes this data (closes the connection and stuff)
        /// </summary>
        public void Dispose()
        {
            SqlReader.Close();
            _conn.Close();
            __underlyingCmd.Dispose();
        }
    }
}
