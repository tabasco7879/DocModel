using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using System.IO;

namespace DocumentModel
{
    abstract class DocModelDB: IDisposable
    {
        protected MongoServer server;
        protected MongoDatabase db;
        protected MongoCollection<BsonDocument> coll;
        protected WordDictionary wordDict;

        public DocModelDB(WordDictionary wd)
        {
            server = MongoServer.Create();
            db = server.GetDatabase(DBName);
            coll = db.GetCollection<BsonDocument>(CollName);
            wordDict = wd;
            docDB = new List<DocModel>();
        }

        public virtual int Count
        {
            get
            {
                int c = 0;
                if (docDB == null)
                {
                    return c;
                }
                return docDB.Count;
            }
        }

        public virtual DocModel this[int idx]
        {
            get { return docDB != null && docDB.Count > idx ? docDB[idx] : null; }
        }

        public abstract string DBName
        {
            get;
        }

        public abstract string CollName
        {
            get;
        }

        public void Dispose()
        {
            coll = null;
            db = null;
            server.Disconnect();
        }        

        public virtual void StoreToDB(BsonDocument doc)
        {
            coll.Insert(doc);
        }

        public abstract DocModel LoadFromDB(BsonDocument bsonDoc);
       
        public virtual void LoadFromDB()
        {           
            MongoCursor<BsonDocument> cursor = coll.FindAll();
            foreach (BsonDocument doc in cursor)
            {
                DocModel docModel = LoadFromDB(doc);
                if (docModel != null)
                {
                    docDB.Add(docModel);
                    if (docDB.Count % 10000 == 0)
                    {
                        Console.WriteLine("Loading {0} records", docDB.Count);
                        break;
                    }
                }                
            }
        }

        public virtual void AddDocModel(DocModel doc)
        {
            docDB.Add(doc);
        } 

        public abstract void Stats(DocModelDictionary classLabelDict);
        protected List<DocModel> docDB;
    }

    class BoWModelDB : DocModelDB
    {
        public BoWModelDB(WordDictionary wd): base(wd)
        {
            
        }

        public override DocModel LoadFromDB(BsonDocument bsonDoc)
        {
            BoWModel docModel = new BoWModel();
            return docModel.LoadFromDB(bsonDoc, wordDict);             
        }
     
        public override string DBName
        {
            get
            {
                return "docmodel";
            }
        }

        public override string CollName
        {
            get
            {
                return "bowmodel";
            }
        }
        
        public override void Stats(DocModelDictionary classLabelDict)
        {
            if (docDB.Count == 0) return;
            Dictionary<string, int> classLabelCounts = new Dictionary<string, int>();
            Dictionary<string, int> wordCounts = new Dictionary<string, int>();
            for (int i = 0; i < docDB.Count; i++)
            {
                BoWModel doc = ((BoWModel)docDB[i]);
                if (doc.ClassLabels != null)
                {
                    foreach (int k in doc.ClassLabels)
                    {
                        int count = 0;
                        string key = classLabelDict.GetKey(k);
                        if (classLabelCounts.TryGetValue(key, out count))
                        {
                            count++;
                            classLabelCounts[key] = count;
                        }
                        else
                        {
                            classLabelCounts.Add(key, 1);
                        }
                    }
                }
                for (int n = 0; n < doc.Length; n++)
                {
                    int count = 0;
                    string key = wordDict.GetKey(doc.Word(n));
                    if (wordCounts.TryGetValue(key, out count))
                    {
                        count += doc.Count(n);
                        wordCounts[key] = count;
                    }
                    else
                    {
                        wordCounts.Add(key, doc.Count(n));
                    }
                }
            }
            List<KeyValuePair<string, int>> orderCounts = classLabelCounts.ToList();
            orderCounts.Sort(
                (x1, x2) =>
                {
                    if (x1.Value > x2.Value)
                        return -1;
                    else if (x1.Value == x2.Value)
                        return 0;
                    else
                        return 1;
                }
                );
            StreamWriter writer = new StreamWriter(new FileStream("classlabel_stats", FileMode.Create));
            for (int i = 0; i < orderCounts.Count; i++)
            {
                writer.WriteLine("{0} : {1}", orderCounts[i].Key, orderCounts[i].Value);            
            }
            writer.Close();

            orderCounts.Clear();
            orderCounts = wordCounts.ToList();
            orderCounts.Sort(
                (x1, x2) =>
                {
                    if (x1.Value > x2.Value)
                        return -1;
                    else if (x1.Value == x2.Value)
                        return 0;
                    else
                        return 1;
                }
                );
            writer = new StreamWriter(new FileStream("classlabel_stats", FileMode.Create));
            for (int i = 0; i < orderCounts.Count; i++)
            {
                writer.WriteLine("{0} : {1}", orderCounts[i].Key, orderCounts[i].Value);
            }
            writer.Close();
        }
    }
}
