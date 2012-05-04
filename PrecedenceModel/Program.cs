using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentModel;

namespace PrecedenceModel
{
    class Program
    {
        static void Main(string[] args)
        {
            ClassLabelDictionary classLabelDict = new ClassLabelDictionary();
            classLabelDict.LoadFromDB();

            TFIDFDictionary tfidfDict = new TFIDFDictionary();
            tfidfDict.LoadFromDB();

            PrecedenceModel precedenceModel = new PrecedenceModel(tfidfDict, classLabelDict);
            precedenceModel.LoadInstances();
            precedenceModel.DiscoverPrecedence();
        }
    }
}
