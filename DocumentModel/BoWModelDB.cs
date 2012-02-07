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
        protected List<DocModel> docDB;
        protected HashSet<string> trainingDocIds;

        public DocModelDB(WordDictionary wd)
        {
            server = MongoServer.Create();
            db = server.GetDatabase(DBName);
            coll = db.GetCollection<BsonDocument>(CollName);
            wordDict = wd;
            docDB = new List<DocModel>();
            LoadTrainingDocIds();
        }

        public void LoadTrainingDocIds()
        {
            if (trainingDocIds == null)
            {
                trainingDocIds = new HashSet<string>();
            }
            else
            {
                trainingDocIds.Clear();
            }

            string line;
            StreamReader reader = new StreamReader(new FileStream("doc_training_stats", FileMode.Open));
            while ((line = reader.ReadLine()) != null)
            {
                trainingDocIds.Add(line.Trim());
            }
            reader.Close();
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
                if (docModel != null && trainingDocIds.Contains(docModel.DocID))
                {
                    docDB.Add(docModel);
                    if (docDB.Count % 10000 == 0)
                    {
                        Console.WriteLine("Loading {0} records", docDB.Count);
                        //break;
                    }
                }                
            }
        }

        public virtual void AddDocModel(DocModel doc)
        {
            docDB.Add(doc);
        } 

        public abstract void Stats(DocModelDictionary classLabelDict);        
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
            Dictionary<string, List<string>> classLabelCounts = new Dictionary<string, List<string>>();
            Dictionary<string, int> wordCounts = new Dictionary<string, int>();
            for (int i = 0; i < docDB.Count; i++)
            {
                BoWModel doc = ((BoWModel)docDB[i]);
                if (doc.ClassLabels != null)
                {
                    foreach (int k in doc.ClassLabels)
                    {                        
                        string key = classLabelDict.GetKey(k);
                        List<string> docIds;
                        if (classLabelCounts.TryGetValue(key, out docIds))                        
                        {
                            docIds = new List<string>();
                        }
                        docIds.Add(doc.DocID);
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
            List<KeyValuePair<string, List<string>>> orderedCLCounts = classLabelCounts.ToList();
            orderedCLCounts.Sort(
                (x1, x2) =>
                {
                    if (x1.Value.Count > x2.Value.Count)
                        return -1;
                    else if (x1.Value.Count == x2.Value.Count)
                        return 0;
                    else
                        return 1;
                }
                );
            StreamWriter writer = new StreamWriter(new FileStream("classlabel_stats", FileMode.Create));
            for (int i = 0; i < orderedCLCounts.Count; i++)
            {
                writer.WriteLine("{0} : {1}", orderedCLCounts[i].Key, orderedCLCounts[i].Value.Count);            
            }
            writer.Close();

            List<KeyValuePair<string, int>> orderedWordCounts = wordCounts.ToList();
            orderedWordCounts.Sort(
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
            writer = new StreamWriter(new FileStream("word_stats", FileMode.Create));
            for (int i = 0; i < orderedWordCounts.Count; i++)
            {
                writer.WriteLine("{0} : {1}", orderedWordCounts[i].Key, orderedWordCounts[i].Value);
            }
            writer.Close();
            orderedWordCounts.Clear();
            wordCounts.Clear();                        
        }

        public void TFIDFFilter()
        {
            Dictionary<int, TFIDF> tfidfDict = new Dictionary<int, TFIDF>();
            foreach (DocModel m in docDB)
            {
                for (int i = 0; i < m.Length; i++)
                {
                    int wordKey = m.Word(i);
                    TFIDF tfidf;
                    if (!tfidfDict.TryGetValue(wordKey, out tfidf))
                    {                        
                        tfidf = new TFIDF(wordKey, docDB.Count);
                        tfidf.key = wordKey;
                        tfidfDict.Add(wordKey, tfidf);
                    }
                    tfidf.tf += m.Count(i);
                    tfidf.df++;
                }
            }

            List<KeyValuePair<int, TFIDF>> tfidfs = tfidfDict.ToList();
            tfidfs.Sort(
                (x1, x2) =>
                {
                    if (x1.Value.tfidf > x2.Value.tfidf)
                    {
                        return -1;
                    }
                    else if (x1.Value.tfidf == x2.Value.tfidf)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                });            
            FilteredDictionary filteredDict = new FilteredDictionary();
            for (int i = 0; i < tfidfs.Count && i < 20000; i++)
            {
                filteredDict.AddValue(wordDict.GetKey(tfidfs[i].Key), tfidfs[i].Key);
            }
            filteredDict.StoreToDB();
        }

        class TFIDF
        {
            public TFIDF(int k, int dc)
            {
                key = k;
                _dc = dc;
                _isCalc = false;
            }

            public int key
            {
                get;
                set;
            }

            public int tf
            {
                get
                {
                    return _tf;
                }
                set
                {
                    _tf = value;
                    _isCalc = false;
                }
            }

            public int df
            {
                get
                {
                    return _df;
                }
                set
                {
                    _df = value;
                    _isCalc = false;
                }
            }

            public double tfidf
            {
                get
                {
                    if(!_isCalc)
                    {
                        _tfidf = _tf * Math.Log((double)(_dc) / _df);
                        _isCalc = true;
                    }
                    return _tfidf;
                }
            }

            private int _tf;
            private int _df;
            private int _dc;
            private double _tfidf;
            private bool _isCalc;
        }
    }
}
