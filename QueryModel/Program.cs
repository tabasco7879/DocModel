using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentModel;
using PModel = PrecedenceModel;

namespace QueryModel
{
    class Program
    {
        static void Main(string[] args)
        {
            ClassLabelDictionary classLabelDict = new ClassLabelDictionary();
            classLabelDict.LoadFromDB();

            WordDictionary wordDict = new WordDictionary();
            wordDict.LoadFromDB();

            TFIDFDictionary tfidfDict = new TFIDFDictionary();
            tfidfDict.LoadFromDB();

            BoWModelDB docDB = new BoWModelDB(wordDict);
            docDB.LoadFromDBByDataSet("doc_set_cls_1000");

            PModel.PrecedenceModel pModel = new PModel.PrecedenceModel(tfidfDict, classLabelDict);
            pModel.DiscoverPrecedence();

            PrecedenceQuery pQuery = new PrecedenceQuery(pModel, wordDict, classLabelDict);
            pQuery.TestQuery(docDB);
        }
    }
}
