using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;

namespace DocumentModel
{
    abstract class DocModel
    {
        public abstract int WordCount { get; }

        public abstract int Length { get; }

        public abstract int Count(int idx);

        public abstract int Word(int idx);

        public abstract string DocID { get; set; }        
    }

    // Bag of word
    class BoWModel : DocModel
    {
        HashSet<int> classLabels;
        Dictionary<int, int> wordCounts;
        List<int> words;
        int totalWordCount;

        public BoWModel()
        {            
        }

        public override string DocID
        {
            get;
            set;
        }

        public override int WordCount
        {
            get
            {
                return totalWordCount;
            }
        }

        public override int Length
        {
            get
            {
                if (wordCounts != null)
                    return wordCounts.Count;
                else
                    return 0;
            }
        }

        public HashSet<int> ClassLabels
        {
            get
            {
                return classLabels;
            }

            set
            {
                classLabels = value;
            }
        }

        public void AddClassLabel(int classLabel)
        {
            if (classLabels == null)
            {
                classLabels = new HashSet<int>();
            }
            classLabels.Add(classLabel);
        }

        public bool HasClassLabel(int classLabel)
        {
            if (classLabels != null)
            {
                return classLabels.Contains(classLabel);
            }
            return false;
        }

        public void AddWord(int word, int freq)
        {
            if (wordCounts == null)
            {
                wordCounts = new Dictionary<int, int>();
            }
            int count = 0;
            if (wordCounts.TryGetValue(word, out count))
            {
                count += freq;
                wordCounts[word] = count;
            }
            else
            {
                wordCounts.Add(word, freq);
            }
            totalWordCount += freq;
        }

        public void AddWord(int word)
        {
            AddWord(word, 1);
        }

        // return word count by word
        public int this[int key]
        {
            get
            {
                int count = 0;
                if (wordCounts != null)
                {
                    wordCounts.TryGetValue(key, out count);
                }
                return count;
            }
        }

        // return word count by index
        public override int Count(int idx)
        {
            int count = 0;
            if (wordCounts != null && words!= null && words.Count>idx)
            {
                wordCounts.TryGetValue(words[idx], out count);
            }
            return count;
        }

        // return word by index
        public override int Word(int idx)
        {
            if (words != null && words.Count>idx)
            {
                return words[idx];
            }
            else
            {
                return -1;
            }
        }

        public BsonDocument StoreToDB()
        {
            BsonDocument doc = new BsonDocument();
            doc["DocID"] = DocID;
            if (classLabels != null)
            {
                doc["ClassLabels"] = new BsonArray(classLabels);
            }
            else
            {                
                doc["ClassLabels"] = BsonNull.Value;
            }
            if (wordCounts != null)
            {
                BsonArray bsonWordCounts = new BsonArray();
                foreach (KeyValuePair<int, int> kvp in wordCounts)
                {
                    BsonDocument bsonKVP = new BsonDocument(kvp.Key.ToString(), kvp.Value);
                    bsonWordCounts.Add(bsonKVP);
                }
                doc["WordCounts"] = bsonWordCounts;
            }
            else
            {
                doc["WordCounts"] = BsonNull.Value;
            }
            return doc;
        }

        public BoWModel LoadFromDB(BsonDocument doc, DocModelDictionary wordDict)
        {            
            DocID = doc["DocID"].AsString;
            if (classLabels != null)
            {
                classLabels.Clear();
            }
            if (!doc["ClassLabels"].IsBsonNull)
            {
                foreach (BsonValue classLabel in doc["ClassLabels"].AsBsonArray)
                {
                    AddClassLabel(classLabel.AsInt32);
                }
            }

            if (wordCounts != null)
            {
                wordCounts.Clear();
            }
            if (!doc["WordCounts"].IsBsonNull)
            {
                foreach (BsonDocument kvp in doc["WordCounts"].AsBsonArray)
                {
                    bool f = true;
                    foreach (BsonElement e in kvp)
                    {
                        int wordKey = int.Parse(e.Name);
                        if (wordDict.GetKey(wordKey) != null)
                        {
                            AddWord(wordKey, e.Value.AsInt32);
                            if (f) { f = false; } else { Debug.Assert(false); }
                        }
                    }
                }
            }

            if (wordCounts == null)
            {
                return null;
            }

            InitIndex();

            return this;
        }

        public void InitIndex()
        {
            if (words != null)
            {
                words.Clear();
            }
            else
            {
                words = new List<int>();
            }
            foreach (KeyValuePair<int, int> kvp in wordCounts)
            {
                words.Add(kvp.Key);
            }
        }
    }
}
