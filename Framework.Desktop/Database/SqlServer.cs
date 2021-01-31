using Npgsql;
using System;

namespace Honjo.Framework.Database
{
    /// <summary>
    /// A sql server, using a single connection
    /// </summary>
    public class SqlServer : IDisposable
    {
        /// <summary>
        /// Given address of the server
        /// </summary>
        protected string Address { get; set; }
        /// <summary>
        /// Connection UID to this server
        /// </summary>
        protected string UID { get; set; }
        /// <summary>
        /// Underlying connection of this server
        /// </summary>
        public NpgsqlConnection Connection { get; protected set; }

        /// <summary>
        /// Constructs a new sql server
        /// </summary>
        /// <param name="address">Address of the server</param>
        /// <param name="database">Name of the database to connect to</param>
        /// <param name="UID">User ID for connection</param>
        /// <param name="password">Password to use for connection</param>
        public SqlServer(string address, string database, string UID, string password) : this(address, database, UID, password, 5432) { }

        /// <summary>
        /// Constructs a new sql server
        /// </summary>
        /// <param name="address">Address of the server</param>
        /// <param name="database">Name of the database to connect to</param>
        /// <param name="UID">User ID for connection</param>
        /// <param name="password">Password to use for connection</param>
        /// <param name="port">The por to connect to. Default is 5432 (pgsql)</param>
        public SqlServer(string address, string database, string UID, string password, int port)
        {
            Address = address;
            this.UID = UID;

            Connection = new NpgsqlConnection("Server=" + address + ";Port="+ port.ToString() + ";Database=" + database + ";User Id=" + UID + ";Password=" + password + ";");

            try
            {
                Connection.Open();
            }
            catch(Exception e)
            {
                Console.WriteLine("External database error when connecting (aborting) : " + e.Message);
                Dispose();
            }
            finally
            {
                Connection.Close();
            }
        }

        /// <summary>
        /// Executes a command in non-query
        /// </summary>
        /// <param name="command">The command (parameters written in order using @@)</param>
        /// <param name="parameters">List of parameters</param>
        public void NonQuery(string command, params object[] parameters) => NonQuery(_ParamFRp(Connection, command, parameters));

        /// <summary>
        /// Executes a command in non-query
        /// </summary>
        /// <param name="command">SQL command to execute</param>
        public void NonQuery(NpgsqlCommand command)
        {
            using (command)
            {
                Connection.Open();
                command.ExecuteNonQuery();
                Connection.Close();
            }
        }

        /// <summary>
        /// Executes a command meant to retrieve data
        /// </summary>
        /// <param name="command">The command (parameters written in order using @@)</param>
        /// <param name="parameters">List of parameters</param>
        /// <returns>The encapsulated data</returns>
        public SqlData Retrieve(string command, params object[] parameters) => Retrieve(_ParamFRp(Connection, command, parameters));

        /// <summary>
        /// Executes a command meant to retrieve data
        /// </summary>
        /// <param name="command">The SQL command</param>
        /// <returns>The encapsulated data</returns>
        public SqlData Retrieve(NpgsqlCommand command)
        {
            Connection.Open();
            return new SqlData(Connection, command.ExecuteReader(), command);
        }

        /// <summary>
        /// Creates a valid sql command from a command string written using @@
        /// </summary>
        protected static NpgsqlCommand _ParamFRp(NpgsqlConnection conn, string command, params object[] parameters) => _ParamReplace(conn, _ParamFormat(command), parameters);

        /// <summary>
        /// Creates a valid sql command from a formatted command string using standard format with parameters
        /// </summary>
        protected static NpgsqlCommand _ParamReplace(NpgsqlConnection conn, string formattedCommand, params object[] parameters)
        {
            NpgsqlCommand scmd = new NpgsqlCommand(formattedCommand, conn);
            for (int i = 0; i < parameters.Length; i++)
                scmd.Parameters.Add(new NpgsqlParameter(i.ToString(), parameters[i] ?? DBNull.Value));

            return scmd;
        }

        /// <summary>
        /// Formats a command written using @@ into a valid sql command with parameter ids
        /// </summary>
        protected static string _ParamFormat(string command)
        {
            string cmd = command;

            if (!command.Contains("@@"))
                return command;

            for (int i = 0, tmp = cmd.IndexOf("@@"); tmp > -1; i++, tmp = cmd.IndexOf("@@"))
                cmd = cmd.Substring(0, tmp) + ("@" + i) + cmd.Substring(tmp + 2);

            return cmd;
        }

        /// <summary>
        /// Closes the connection and frees memory
        /// </summary>
        public void Dispose() => Connection.Dispose();
    }
}
