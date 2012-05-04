using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using System.IO;

namespace DocumentModel
{
    class Program
    {        
        static void Main(string[] args)
        {                                               
            //LDAEstimate(116);
            //Stats();
            //GenerateTFIDFDictionary();
            //CompileDataSet();
            int[] listOfTopicNumbers = {100, 80, 50, 30, 20, 10};
            for (int i = 0; i < listOfTopicNumbers.Length; i++)
            {
                LDAEstimateDataSet("doc_set_cls_1000", listOfTopicNumbers[i]);
            }
        }

        // generate a compact dictionary using TFIDF measure
        static void GenerateTFIDFDictionary()
        {
            InitDict();
            BoWModelDB docDB = new BoWModelDB(wordDict);
            docDB.LoadFromDB();
            docDB.GenerateTFIDFDictionary();                        
        }

        static void CompileDataSet()
        {
            InitTFIDFDict();
            BoWModelDB docDB = new BoWModelDB(tfidfDict);
            docDB.LoadFromDB();
            Dictionary<int, int> clsCounts = new Dictionary<int, int>();
            for (int i = 0; i < docDB.Count; i++)
            {
                if (((BoWModel)docDB[i]).ClassLabels != null)
                {
                    foreach (int cls in ((BoWModel)docDB[i]).ClassLabels)
                    {
                        int count = 0;
                        if (clsCounts.TryGetValue(cls, out count))
                            clsCounts[cls] = count + 1;
                        else
                            clsCounts.Add(cls, 1);
                    }
                }
            }

            StreamWriter writer = new StreamWriter(new FileStream("doc_set_cls_1000", FileMode.Create));
            for (int i = 0; i < docDB.Count; i++)
            {
                bool selected = false;
                if (((BoWModel)docDB[i]).ClassLabels != null)
                {
                    foreach (int cls in ((BoWModel)docDB[i]).ClassLabels)
                    {
                        if (clsCounts[cls] >= 1000)
                        {
                            selected = true;
                            break;
                        }
                    }
                }
                if (selected)
                {
                    writer.WriteLine(docDB[i].DocID);
                }
            }
            writer.Close();
        }

        static void LDAEstimateDataSet(string dataSetName, int numOfTopics)
        {
            InitTFIDFDict();
            docModelDB = new LDABoWModelDB(numOfTopics, tfidfDict);
            docModelDB.LDACollectionName = "ldadocs_" + numOfTopics;
            docModelDB.LoadFromDBByDataSet(dataSetName);
            docModelDB.Init();
            docModelDB.RunEM(true);
            docModelDB.SaveLDAModel();
            docModelDB = null;
        }              

        static void Stats()
        {
            InitDict();
            docModelDB = new LDABoWModelDB(1, wordDict);
            docModelDB.LoadFromDB();
            // print class labels
            docModelDB.Stats(classLabelDict);
        }

        // Load data from original text file
        static void LoadFromDB()
        {            
            MongoServer server = MongoServer.Create();
            MongoDatabase db = server.GetDatabase("pubmed");
            MongoCollection<BsonDocument> coll = db.GetCollection<BsonDocument>("patientcare");
            MongoCursor cursor = coll.FindAll();            

            int count = 0;
            foreach (BsonDocument article in cursor)
            {
                BoWModel bowDoc = new BoWModel();
                bowDoc.DocID = article["ArticleId"].AsString;
                if (!article["Title"].IsBsonNull)
                {
                    SplitWords(article["Title"].AsString, bowDoc);
                }
                if (!article["MeshHeadings"].IsBsonNull)
                {
                    foreach (BsonValue s in article["MeshHeadings"].AsBsonArray)
                    {
                        string s1 = s.AsString.Replace(".", "");
                        int classLabel = classLabelDict.GetValue(s1);
                        bowDoc.AddClassLabel(classLabel);
                    }
                }
                if (!article["AbstractTexts"].IsBsonNull)
                {
                    foreach (BsonDocument s in article["AbstractTexts"].AsBsonArray)
                    {
                        if (!s["Value"].IsBsonNull)
                        {
                            SplitWords(s["Value"].AsString, bowDoc);
                        }
                    }
                }

                docModelDB.StoreToDB(bowDoc.StoreToDB());

                count++;
                if (count % 1000 ==0)
                {
                    Console.WriteLine("Loading {0} records", count);
                }

            }
            coll = null;
            db = null;            

            server.Disconnect();
            classLabelDict.StoreToDB();
            wordDict.StoreToDB();
        }

        static void SplitWords(string s, BoWModel docModel)
        {
            string l = s.ToLower();
            Match m;
            Regex r = new Regex(@"[a-zA-Z]+[0-9]*");
            for (m = r.Match(l); m.Success; m = m.NextMatch())
            {
                int word = wordDict.GetValue(Detachment.Instance.Detach(m.Value));
                docModel.AddWord(word);
            }
        }       

        static void InitDict()
        {
            classLabelDict = new ClassLabelDictionary();
            classLabelDict.LoadFromDB();

            wordDict = new WordDictionary();
            wordDict.LoadFromDB();                                    
        }

        static void InitTFIDFDict()
        {
            classLabelDict = new ClassLabelDictionary();
            classLabelDict.LoadFromDB();

            tfidfDict = new TFIDFDictionary();
            tfidfDict.LoadFromDB();                                    
        }

        static void InitAP()
        {
            wordDict = new WordDictionary();
            docModelDB = new LDABoWModelDB(10, wordDict);

            string line;
            StreamReader reader = new StreamReader(new FileStream("ap.dat", FileMode.Open));
            while ((line = reader.ReadLine()) != null)
            {
                string[] ss = line.Split(' ');
                LDABoWModel doc = new LDABoWModel(docModelDB);                
                for (int i = 1; i < ss.Length; i++)
                {
                    string[] sss = ss[i].Split(':');
                    doc.AddWord(int.Parse(sss[0]), int.Parse(sss[1]));
                    wordDict.AddValue(sss[0], int.Parse(sss[0]));
                }
                doc.InitIndex();
                docModelDB.AddDocModel(doc);                
            }
            reader.Close();
            docModelDB.Init();
        }

        static List<object> detachRule = new List<object>();
        static ClassLabelDictionary classLabelDict;
        static WordDictionary wordDict;
        static TFIDFDictionary tfidfDict;
        static LDABoWModelDB docModelDB;
    }    
}
