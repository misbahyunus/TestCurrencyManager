using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TestCurrencyManager.Wpf.Model;

namespace TestCurrencyManager.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields
        // Database File Location
        private string dbFile = @"C:\Temp\test.db";
        // Database Connection String
        private string connString = string.Empty;
        // Dictionary which stores currency list
        Dictionary<string, string> currencyLookupDict;
        // List which stores latest exchange rates
        List<ExchangeRate> list = new List<ExchangeRate>();
        #endregion

        #region Constructor
        /// <summary>
        /// Default Constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            // Get currency lookup data
            currencyLookupDict = CreateCurrencyLookup();
            // Fill combobox with data
            InitializeCombobox();
            // Set up connection to the SQLite database
            connString = string.Format(@"Data Source={0}; Pooling=false; FailIfMissing=false;", dbFile);            
        }
        #endregion

        #region Methods
        /// <summary>
        /// Fetches and displays the latest exchnage rates for selected base currency
        /// </summary>
        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            if(cbCurrency.SelectedIndex > 0)
            {
                string input = cbCurrency.SelectedValue.ToString();
                Cursor = Cursors.Wait;
                ProcessSelection(input);
                Cursor = Cursors.Arrow;
                DisplayCurrenyRate(connString, input);
            }            
        }

        /// <summary>
        /// Closes the window
        /// </summary>
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Loads the combobox with available list of currencies to choose from
        /// </summary>
        private void InitializeCombobox()
        {
            cbCurrency.DisplayMemberPath = "Value";
            cbCurrency.SelectedValuePath = "Key";
            cbCurrency.ItemsSource = currencyLookupDict;
            cbCurrency.SelectedIndex = 0;
        }

        /// <summary>
        /// Does processing for selected currency code
        /// </summary>
        /// <param name="paraInputCurrencyCode">Base currency code</param>
        private void ProcessSelection(string paraInputCurrencyCode)
        {
            if (!File.Exists(dbFile))
            {
                SQLiteConnection.CreateFile(dbFile);
                InitializeDatabase(connString);
                InsertBaseCurrency(connString, currencyLookupDict, paraInputCurrencyCode);
                InsertCurrency(connString, currencyLookupDict, paraInputCurrencyCode);
            }
            else
            {
                if (paraInputCurrencyCode != GetCurrentBaseCurrency(connString))
                {
                    Console.WriteLine("Base currency changed. Loading fresh values..");
                    File.Delete(dbFile);
                    SQLiteConnection.CreateFile(dbFile);
                    InitializeDatabase(connString);
                    InsertBaseCurrency(connString, currencyLookupDict, paraInputCurrencyCode);
                    InsertCurrency(connString, currencyLookupDict, paraInputCurrencyCode);
                }
                else
                {
                    // Fetch latest currency rates
                    UpdateCurrencyRate(connString, paraInputCurrencyCode);
                }
            }
        }

        #region Database Methods
        /// <summary>
        /// Creates new SQL database structure
        /// </summary>
        /// <param name="connString">SQL Connection String</param>
        private void InitializeDatabase(string connString)
        {
            //Console.WriteLine("Setting up database..");
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
            //Console.WriteLine("Setup finished..");
            Console.WriteLine();
        }

        /// <summary>
        /// Populates the SQL database with latest currency exchange rates
        /// </summary>
        /// <param name="connString">SQL Connection String</param>
        /// <param name="paraDict">Lookup dictionary containing currency code and description</param>
        private void InsertCurrency(string connString, Dictionary<string, string> paraDict, string paraBaseCurrency)
        {
            //Console.WriteLine("Begin InsertCurrency");

            var url = "https://api.fixer.io/latest?base=" + paraBaseCurrency;
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
        /// Inserts the base currency with exchange rate = 1
        /// </summary>
        /// <param name="connString">SQL Connection String</param>
        /// <param name="paraDict">Lookup dictionary containing currency code and description</param>
        private void InsertBaseCurrency(string connString, Dictionary<string, string> paraDict, string paraBaseCurrency)
        {
            using (var factory = new SQLiteFactory())
            {
                using (System.Data.Common.DbConnection dbConn = factory.CreateConnection())
                {
                    dbConn.ConnectionString = connString;
                    dbConn.Open();
                    using (System.Data.Common.DbCommand cmd = dbConn.CreateCommand())
                    {
                        string description = string.Empty;
                        if (paraDict.ContainsKey(paraBaseCurrency))
                        {
                            description = paraDict[paraBaseCurrency];

                            //parameterized insert
                            cmd.CommandText = @"INSERT INTO Currencies ([Code],[Description],[ExchangeRate]) VALUES(@code,@description,'1.00');";

                            var p1 = cmd.CreateParameter();
                            p1.ParameterName = "@code";
                            p1.Value = paraBaseCurrency;

                            var p2 = cmd.CreateParameter();
                            p2.ParameterName = "@description";
                            p2.Value = description;

                            cmd.Parameters.Add(p1);
                            cmd.Parameters.Add(p2);

                            cmd.ExecuteNonQuery();
                        }
                    }
                    if (dbConn.State != System.Data.ConnectionState.Closed) dbConn.Close();
                }
            }
            //Console.WriteLine("End InsertCurrency");
        }

        /// <summary>
        /// Updates the local database file with latest currency rates
        /// </summary>
        /// <param name="connString"></param>
        private void UpdateCurrencyRate(string connString, string paraBaseCurrency)
        {
            Console.WriteLine("Updating currency rates...");
            var url = "https://api.fixer.io/latest?base=" + paraBaseCurrency;
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
        /// Gets the current base currency in the system
        /// </summary>
        /// <param name="connString"></param>
        /// <returns></returns>
        private string GetCurrentBaseCurrency(string connString)
        {
            string result = string.Empty;

            using (var factory = new SQLiteFactory())
            {
                using (System.Data.Common.DbConnection dbConn = factory.CreateConnection())
                {
                    dbConn.ConnectionString = connString;
                    dbConn.Open();
                    using (System.Data.Common.DbCommand cmd = dbConn.CreateCommand())
                    {
                        //read from the table
                        cmd.CommandText = @"SELECT [Code] FROM Currencies WHERE [ExchangeRate] = 1.00;";
                        using (System.Data.Common.DbDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result = reader.GetString(0);
                            }
                        }
                        cmd.Dispose();
                    }
                    if (dbConn.State != System.Data.ConnectionState.Closed) dbConn.Close();
                }
            }

            return result;
        }

        /// <summary>
        /// Displays the currency rates stored in the local database
        /// </summary>
        /// <param name="connString"></param>
        private void DisplayCurrenyRate(string connString, string paraBaseCurrency)
        {
            txtblkResult.Text = "1 " + paraBaseCurrency + " is ";
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
                                string currencyInfo = description + "(" + code + ")";
                                decimal currencyRate = reader.GetDecimal(2);
                                // Do not display base rate in the list
                                if (code == paraBaseCurrency) { continue; }
                                list.Add(new ExchangeRate() { Rate = currencyRate, Currency = currencyInfo });
                            }
                        }
                        cmd.Dispose();
                    }
                    if (dbConn.State != System.Data.ConnectionState.Closed) dbConn.Close();
                }
            }
            // Set listview source to latest list of exchnage rates
            lvDataBinding.ItemsSource = list;
        }
        #endregion

        /// <summary>
        /// Returns a dictionary containing currency code and their description
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> CreateCurrencyLookup()
        {
            Dictionary<string, string> temp = new Dictionary<string, string>();
            temp.Add("XYZ", "Please Select Base Currency");
            temp.Add("USD", "(USD) US Dollar");
            temp.Add("GBP", "(GBP) British Pound");
            temp.Add("NZD", "(NZD) New Zealand Dollar");
            temp.Add("INR", "(INR) Indian Rupee");
            temp.Add("EUR", "(EUR) Euro");
            temp.Add("JPY", "(JPY) Japanese Yen");
            temp.Add("CAD", "(CAD) Canadian Dollar");
            temp.Add("CNY", "(CNY) Chinese Yuan");
            temp.Add("HKD", "(HKD) Hong Kong Dollar");
            temp.Add("IDR", "(IDR) Indonesian Rupiah");
            temp.Add("MYR", "(MYR) Malaysian Ringgit");
            temp.Add("SGD", "(SGD) Singapore Dollar");
            temp.Add("AUD", "(AUD) Australian Dollar");

            return temp;
        }
                
    }
    #endregion

    public class ExchangeRate
    {
        public decimal Rate { get; set; }
        public string Currency { get; set; }
    }
}
