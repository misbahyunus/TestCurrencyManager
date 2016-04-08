using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestCurrencyManager.Model;

namespace TestCurrencyManager
{
    class Program
    {
        static void Main(string[] args)
        {
            var dbFile = @"C:\Temp\test.db";
            var connString = string.Format(@"Data Source={0}; Pooling=false; FailIfMissing=false;", dbFile);

            var currencyLoookup = CreateCurrencyLookup();
                        
            if (!File.Exists(dbFile))
            {
                SQLiteConnection.CreateFile(dbFile);
                InitializeDatabase(connString);
                InsertCurrency(connString, currencyLoookup);
            }

            DisplayCurrenyRate(connString);

            //Console.WriteLine("Deleting DB file now..");
            //if (File.Exists(dbFile))
            //{
            //    File.Delete(dbFile);
            //}

            Console.ReadLine();
        }

        /// <summary>
        /// Creates new SQL database structure
        /// </summary>
        /// <param name="connString">SQL Connection String</param>
        private static void InitializeDatabase(string connString)
        {
            Console.WriteLine("Setting up database..");
            using (var factory = new SQLiteFactory())
            {
                using (System.Data.Common.DbConnection dbConn = factory.CreateConnection())
                {
                    // This is the query which will create a new table in our database file with three columns.
                    string createTableQuery = @"CREATE TABLE IF NOT EXISTS [Currencies] (
                          [Code] VARCHAR(3)  NOT NULL PRIMARY KEY,
                          [Description] VARCHAR(30)  NOT NULL,
                          [ExchangeRate] [decimal](16, 6) NOT NULL
                          )";

                    dbConn.ConnectionString = connString;
                    dbConn.Open();
                    using (System.Data.Common.DbCommand cmd = dbConn.CreateCommand())
                    {
                        //create table
                        cmd.CommandText = createTableQuery;
                        cmd.ExecuteNonQuery();
                        
                        cmd.Dispose();
                    }
                    if (dbConn.State != System.Data.ConnectionState.Closed) dbConn.Close();
                }
            }
            Console.WriteLine("Setup finished..");
            Console.WriteLine();
        }

        /// <summary>
        /// Populates the SQL database with latest currency exchange rates
        /// </summary>
        /// <param name="connString">SQL Connection String</param>
        /// <param name="paraDict">Lookup dictionary containing currency code and description</param>
        private static void InsertCurrency(string connString, Dictionary<string, string> paraDict)
        {
            //Console.WriteLine("Begin InsertCurrency");

            var url = "https://api.fixer.io/latest?base=AUD";
            var currencyRates = FetchJson._download_serialized_json_data<Currency>(url);

            using (var factory = new SQLiteFactory())
            {
                using (System.Data.Common.DbConnection dbConn = factory.CreateConnection())
                {
                    dbConn.ConnectionString = connString;
                    dbConn.Open();
                    using (System.Data.Common.DbCommand cmd = dbConn.CreateCommand())
                    {
                        foreach (KeyValuePair<string, decimal> entry in currencyRates.rates)
                        {
                            string description = string.Empty;
                            if (paraDict.ContainsKey(entry.Key))
                            {
                                description = paraDict[entry.Key];

                                //parameterized insert
                                cmd.CommandText = @"INSERT INTO Currencies ([Code],[Description],[ExchangeRate]) VALUES(@code,@description,@rate);";

                                var p1 = cmd.CreateParameter();
                                p1.ParameterName = "@code";
                                p1.Value = entry.Key;

                                var p2 = cmd.CreateParameter();
                                p2.ParameterName = "@description";
                                p2.Value = description;

                                var p3 = cmd.CreateParameter();
                                p3.ParameterName = "@rate";
                                p3.Value = entry.Value;

                                cmd.Parameters.Add(p1);
                                cmd.Parameters.Add(p2);
                                cmd.Parameters.Add(p3);

                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    if (dbConn.State != System.Data.ConnectionState.Closed) dbConn.Close();
                }
            }
            //Console.WriteLine("End InsertCurrency");
        }

        /// <summary>
        /// Displays the currency rates stored in the local database
        /// </summary>
        /// <param name="connString"></param>
        private static void DisplayCurrenyRate(string connString)
        {
            Console.WriteLine("--------");
            Console.WriteLine("1 AUD is ");
            Console.WriteLine("--------");
            using (var factory = new SQLiteFactory())
            {
                using (System.Data.Common.DbConnection dbConn = factory.CreateConnection())
                {
                    dbConn.ConnectionString = connString;
                    dbConn.Open();
                    using (System.Data.Common.DbCommand cmd = dbConn.CreateCommand())
                    {
                        //read from the table
                        cmd.CommandText = @"SELECT [Code], [Description], [ExchangeRate] FROM Currencies ORDER BY [Code];";
                        using (System.Data.Common.DbDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string code = reader.GetString(0);
                                string description = reader.GetString(1);
                                string currency = description + "(" + code + ")";
                                decimal rate = reader.GetDecimal(2);
                                Console.WriteLine("       = {0} {1}", rate, currency);
                            }
                        }
                        cmd.Dispose();
                    }
                    if (dbConn.State != System.Data.ConnectionState.Closed) dbConn.Close();
                }
            }
            Console.WriteLine("*******************");
        }

        /// <summary>
        /// Updates the local database file with latest currency rates
        /// </summary>
        /// <param name="connString"></param>
        private static void UpdateCurrencyRate(string connString)
        {
            Console.WriteLine("Updating currency rates...");
            var url = "https://api.fixer.io/latest?base=AUD";
            var currencyRates = FetchJson._download_serialized_json_data<Currency>(url);

            using (var factory = new SQLiteFactory())
            {
                using (System.Data.Common.DbConnection dbConn = factory.CreateConnection())
                {
                    dbConn.ConnectionString = connString;
                    dbConn.Open();
                    using (System.Data.Common.DbCommand cmd = dbConn.CreateCommand())
                    {
                        foreach (KeyValuePair<string, decimal> entry in currencyRates.rates)
                        {
                            //parameterized insert
                            cmd.CommandText = @"UPDATE Currencies SET [ExchangeRate] = @rate WHERE [code] = @code;";

                            var p1 = cmd.CreateParameter();
                            p1.ParameterName = "@code";
                            p1.Value = entry.Key;

                            var p2 = cmd.CreateParameter();
                            p2.ParameterName = "@rate";
                            p2.Value = entry.Value;

                            cmd.Parameters.Add(p1);
                            cmd.Parameters.Add(p2);

                            cmd.ExecuteNonQuery();
                        }
                    }
                    if (dbConn.State != System.Data.ConnectionState.Closed) dbConn.Close();
                }
                Console.WriteLine("Finished updating currency rates...");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Returns a dictionary containing currency code and their description
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, string> CreateCurrencyLookup()
        {
            Dictionary<string, string> temp = new Dictionary<string, string>();

            temp.Add("USD", "US Dollar");
            temp.Add("GBP", "British Pound");
            temp.Add("NZD", "New Zealand Dollar");
            temp.Add("INR", "Indian Rupee");
            temp.Add("EUR", "Euro");
            temp.Add("JPY", "Japanese Yen");
            temp.Add("CAD", "Canadian Dollar");
            temp.Add("CNY", "Chinese Yuan");
            temp.Add("HKD", "Hong Kong Dollar");
            temp.Add("IDR", "Indonesian Rupiah");
            temp.Add("MYR", "Malaysian Ringgit");
            temp.Add("SGD", "Singapore Dollar");
            temp.Add("AUD", "Australian Dollar");

            return temp;
        }
    }
}
