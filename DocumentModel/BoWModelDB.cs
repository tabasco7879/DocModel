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
        protected HashSet<string> validDocIds;

        public DocModelDB(WordDictionary wd)
        {
            server = MongoServer.Create();
            db = server.GetDatabase(DBName);
            coll = db.GetCollection<BsonDocument>(CollName);
            wordDict = wd;
            docDB = new List<DocModel>();
            validDocIds = new HashSet<string>();
        }

        public void LoadFromDBByClass(int classKey)
        {
            docDB.Clear();
            string line;
            StreamReader reader = new StreamReader(new FileStream("training_data//doc_training_stats_" + classKey, FileMode.Open));
            QueryDocument query;            
            while ((line = reader.ReadLine()) != null)
            {                        
                query = new QueryDocument("DocID", line.Trim());                           

                DocModel docModel = LoadFromDB(coll.FindOne(query));
                docDB.Add(docModel);
                if (docDB.Count % 1000 == 0)
                {
                    Console.WriteLine("Loading {0} records", docDB.Count);                 
                }
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
                if (docModel != null
                    && (validDocIds.Count == 0 || validDocIds.Contains(docModel.DocID)))
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
            Dictionary<int, HashSet<string>> classLabelCounts = new Dictionary<int, HashSet<string>>();
            Dictionary<int, int> wordCounts = new Dictionary<int, int>();
            for (int i = 0; i < docDB.Count; i++)
            {
                BoWModel doc = ((BoWModel)docDB[i]);
                if (doc.ClassLabels != null)
                {
                    foreach (int k in doc.ClassLabels)
                    {                        
                        //string key = classLabelDict.GetKey(k);
                        HashSet<string> docIds;
                        if (!classLabelCounts.TryGetValue(k, out docIds))                        
                        {
                            docIds = new HashSet<string>();
                            classLabelCounts.Add(k, docIds);
                        }
                        docIds.Add(doc.DocID);
                    }
                }
                for (int n = 0; n < doc.Length; n++)
                {
                    int count = 0;
                    //string key = wordDict.GetKey(doc.Word(n));
                    if (wordCounts.TryGetValue(doc.Word(n), out count))
                    {
                        count += doc.Count(n);
                        wordCounts[doc.Word(n)] = count;
                    }
                    else
                    {
                        wordCounts.Add(doc.Word(n), doc.Count(n));
                    }
                }
            }
            List<KeyValuePair<int, HashSet<string>>> orderedCLCounts = classLabelCounts.ToList();
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
                writer.WriteLine("{0} : {1}", classLabelDict.GetKey(orderedCLCounts[i].Key), orderedCLCounts[i].Value.Count);            
            }
            writer.Close();

            List<KeyValuePair<int, int>> orderedWordCounts = wordCounts.ToList();
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
                writer.WriteLine("{0} : {1}", wordDict.GetKey(orderedWordCounts[i].Key), orderedWordCounts[i].Value);
            }
            writer.Close();
            orderedWordCounts.Clear();
            wordCounts.Clear();

            List<string> candiates = new List<string>();
            for (int i = 0; i < docDB.Count; i++)
            {
                BoWModel doc = ((BoWModel)docDB[i]);
                if (doc.ClassLabels != null)
                {
                    foreach (int k in doc.ClassLabels)
                    {
                        if (classLabelCounts[k].Count > 900)
                        {
                            candiates.Add(doc.DocID);
                        }
                        break;
                    }
                }
            }

            writer = new StreamWriter(new FileStream("doc_stats", FileMode.Create));
            for (int i = 0; i < candiates.Count; i++)
            {                
                writer.WriteLine("{0}", candiates[i]);                
            }
            writer.Close();
            
            foreach (KeyValuePair<int, HashSet<string>> kvp in classLabelCounts)
            {
                if (kvp.Value.Count > 900)
                {
                    HashSet<string> training = new HashSet<string>();
                    HashSet<string> crsvalid = new HashSet<string>();
                    HashSet<string> testing = new HashSet<string>();
                    int trainingSize = (int)(kvp.Value.Count * 0.7);
                    int crsvalidSize = (int)(kvp.Value.Count * 0.1);
                    int testingSize = kvp.Value.Count - trainingSize - crsvalidSize;
                    int i = 0;
                    foreach (string s in kvp.Value)
                    {
                        if (i < trainingSize)
                        {
                            training.Add(s);
                        }
                        else if (i < trainingSize + crsvalidSize)
                        {
                            crsvalid.Add(s);
                        }
                        else
                        {
                            testing.Add(s);
                        }                                     
                    }
                    HashSet<string> usedDocs = new HashSet<string>();

                    RandomlyFill(training, kvp.Value, usedDocs, candiates, 7000);
                    RandomlyFill(crsvalid, kvp.Value, usedDocs, candiates, 1000);
                    RandomlyFill(testing, kvp.Value, usedDocs, candiates, 2000);

                    writer = new StreamWriter(new FileStream("training_data\\doc_training_stats_" + kvp.Key, FileMode.Create));
                    foreach(string s in training)
                    {
                        writer.WriteLine("{0}", s);
                    }
                    writer.Close();

                    writer = new StreamWriter(new FileStream("crsvalid_data\\doc_crsvalid_stats_" + kvp.Key, FileMode.Create));
                    foreach (string s in crsvalid)
                    {
                        writer.WriteLine("{0}", s);
                    }
                    writer.Close();

                    writer = new StreamWriter(new FileStream("testing_data\\doc_testing_stats_" + kvp.Key, FileMode.Create));
                    foreach (string s in testing)
                    {
                        writer.WriteLine("{0}", s);
                    }
                    writer.Close();
                }
            }
        }

        private void RandomlyFill(HashSet<string> fillingSet, HashSet<string> exclusionSet,
            HashSet<string> usedSet, List<string> candidateSet, int bound)
        {
            Random rand = new Random();
            while (fillingSet.Count < bound)
            {
                int idx = (int)Math.Floor(rand.NextDouble() * candidateSet.Count);
                if (!exclusionSet.Contains(candidateSet[idx]) && !usedSet.Contains(candidateSet[idx]))
                {
                    fillingSet.Add(candidateSet[idx]);
                    usedSet.Add(candidateSet[idx]);
                }
            }
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
