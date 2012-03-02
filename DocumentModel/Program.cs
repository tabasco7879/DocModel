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
            LDAEstimate(116);
            //Stats();
        }

        static void LDAEstimate()
        {
            InitDict();
            docModelDB = new LDABoWModelDB(50, wordDict);
            //docModelDB.LoadFromDB();
            // LDA
            string[] classLabels = Directory.GetFiles("training_data");
            for (int i = 0; i < classLabels.Length; i++)
            {
                int classKey = int.Parse(
                    classLabels[i].Substring(classLabels[i].LastIndexOf('_') + 1)
                    );
                docModelDB.LoadFromDBByClass(classKey, "training");
                docModelDB.Init();
                docModelDB.RunEM();
                docModelDB.SaveModel(classKey);
                docModelDB.StoreLDA(classKey, "training");
                // inference on cross validation data
                docModelDB.LoadFromDBByClass(classKey, "crsvalid");
                docModelDB.E_Step();
                docModelDB.StoreLDA(classKey, "crsvalid");
            }
        }

        static void LDAEstimate(int classKey)
        {
            InitDict();
            docModelDB = new LDABoWModelDB(50, wordDict);            
            // LDA            
            docModelDB.LoadFromDBByClass(classKey, "training");
            docModelDB.Init();
            docModelDB.RunEM();
            docModelDB.SaveModel(classKey);
            docModelDB.StoreLDA(classKey, "training");
            // inference on cross validation data
            docModelDB.LoadFromDBByClass(classKey, "crsvalid");
            docModelDB.E_Step();
            docModelDB.StoreLDA(classKey, "crsvalid");            
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
        static LDABoWModelDB docModelDB;
    }    
}
