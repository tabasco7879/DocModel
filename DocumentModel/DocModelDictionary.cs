using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using System.IO;
using System.Diagnostics;

namespace DocumentModel
{
    abstract class DocModelDictionary
    {
        Dictionary<string, int> dict;
        Dictionary<int, string> inverseDict;

        public int GetValue(string s)
        {            
            if (dict == null)
            {
                dict = new Dictionary<string, int>();
                inverseDict = new Dictionary<int, string>();
            }
            int value = -1;
            if (!dict.TryGetValue(s, out value))
            {
                value = dict.Count;
                dict.Add(s, value);
                inverseDict.Add(value, s);
            }
            return value;
        }

        public void AddValue(string s, int v)
        {
            if (dict == null)
            {
                dict = new Dictionary<string, int>();
                inverseDict = new Dictionary<int, string>();
            }
            if (!dict.ContainsKey(s))
            {
                dict.Add(s, v);
                inverseDict.Add(v, s);
            }
            else
            {
                Debug.Assert(dict[s] == v);
            }
        }

        public string GetKey(int v)
        {
            if (inverseDict == null)
                return null;
            else
            {
                string key = null;
                inverseDict.TryGetValue(v, out key);
                return key;
            }
        }

        public abstract string DBName
        {
            get;
        }

        public abstract string CollName
        {
            get;
        }

        public Dictionary<string, int> Dictionary
        {
            get
            {
                return dict;
            }
        }

        public abstract bool LoadFilter(string key);        

        public void LoadFromDB()
        {            
            MongoServer server = MongoServer.Create();
            MongoDatabase db = server.GetDatabase(DBName);
            MongoCollection<BsonDocument> coll = db.GetCollection<BsonDocument>(CollName);
            MongoCursor<BsonDocument> cursor = coll.FindAll();
            if (dict == null)
            {
                dict = new Dictionary<string, int>();
                inverseDict = new Dictionary<int, string>();
            }
            else
            {
                dict.Clear();
                inverseDict.Clear();
            }
            foreach (BsonDocument kvp in cursor)
            {
                bool f = true;
                foreach (BsonElement e in kvp)
                {
                    if (f) { f = false; continue; }
                    if(LoadFilter(e.Name))
                    {
                        dict.Add(e.Name, e.Value.AsInt32);
                        inverseDict.Add(e.Value.AsInt32, e.Name);
                    }
                }                
            }
            cursor = null;
            coll = null;
            db = null;
            server.Disconnect();
        }

        public void StoreToDB()
        {
            if (dict != null && dict.Count > 0)
            {
                MongoServer server = MongoServer.Create();
                MongoDatabase db = server.GetDatabase(DBName);
                MongoCollection<BsonDocument> coll = db.GetCollection<BsonDocument>(CollName);
                coll.RemoveAll();
                foreach (KeyValuePair<string, int> pair in dict)
                {
                    BsonDocument rec = new BsonDocument(pair.Key, pair.Value);                    
                    coll.Insert(rec);
                }
                coll = null;
                db = null;
                server.Disconnect();
            }
        }

        public DocModelDictionary()
        {
            
        }
    }

    class WordDictionary : DocModelDictionary
    {

        HashSet<string> stopwords;
        HashSet<string> infrequentwords;

        public override string CollName
        {
            get { return "worddict"; }
        }

        public override string DBName
        {
            get { return "docmodel"; }
        }

        public override bool LoadFilter(string key)
        {
            if (key.Trim().Length == 0 || key.Trim().Length==1) return false;            
            if (stopwords == null)
            {
                LoadStopWords();                    
            }
            if (stopwords.Contains(key))
            {
                return false;
            }
            else
            {
                if (infrequentwords == null)
                {
                    LoadInfrequentWords();
                }
                return !infrequentwords.Contains(key);
            }
        }

        public void LoadStopWords()
        {
            if (stopwords == null)
            {
                stopwords = new HashSet<string>();
            }
            else
            {
                stopwords.Clear();
            }

            string line;
            StreamReader reader = new StreamReader(new FileStream("stopword.txt", FileMode.Open));
            if ((line = reader.ReadLine()) != null)
            {
                string[] ss = line.Split(',');
                for (int i = 0; i < ss.Length; i++)
                {
                    stopwords.Add(ss[i]);        
                }
            }
            reader.Close();
        }

        public void LoadInfrequentWords()
        {
            if (infrequentwords == null)
            {
                infrequentwords = new HashSet<string>();
            }
            else
            {
                infrequentwords.Clear();
            }

            string line;
            StreamReader reader = new StreamReader(new FileStream("infrequentword.txt", FileMode.Open));
            while ((line = reader.ReadLine()) != null)
            {
                string[] ss = line.Split(':');
                infrequentwords.Add(ss[0].Trim());                
            }
            reader.Close();
        }

    }

    class ClassLabelDictionary : DocModelDictionary
    {
        public override string CollName
        {
            get { return "classlabel"; }
        }

        public override string DBName
        {
            get { return "docmodel"; }
        }

        public override bool LoadFilter(string key)
        {
            return true;
        }
    }

    class FilteredDictionary : DocModelDictionary
    {
        public override string CollName
        {
            get { return "filtereddict"; }
        }

        public override string DBName
        {
            get { return "docmodel"; }
        }

        public override bool LoadFilter(string key)
        {
            return true;
        }
    }
}
