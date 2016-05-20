using System.Collections.Generic;

namespace TestCurrencyManager.Wpf.Model
{
    public class Currency
    {
        public string _base { get; set; }
        public string date { get; set; }
        public Dictionary<string, decimal> rates { get; set; }
    }
}
