using System;
using System.Collections.Generic;
using System.Text;

namespace FactoorSharp.FacturXDocumentationParser
{
    public class Cardinality
    {
        public string MinOccurs { get; set; }
        public string MaxOccurs { get; set; }


        public Cardinality()
        {
        } // !Cardinality()


        public Cardinality(string minOccurs, string maxOccurs)
        {
            MinOccurs = minOccurs;
            MaxOccurs = maxOccurs;
        } // !Cardinality()


        public override string ToString()
        {
            return $"{MinOccurs}..{MaxOccurs}";
        } // !ToString()


        public static Cardinality FromString(string s)
        {
            if (String.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            string[] parts = s.Split(new[] { ".." }, StringSplitOptions.None);
            return new Cardinality
            {
                MinOccurs = parts.Length > 0 ? parts[0] : "1",
                MaxOccurs = parts.Length > 1 ? parts[1] : "1",
            };
        } // !FromString()
    }
}
