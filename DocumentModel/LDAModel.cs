using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;

namespace DocumentModel
{
    public class LDAModel : DocModel
    {
        HashSet<int> classLabels;
        Dictionary<int, double> wordWeights;        

        public override int WordCount
        {
            get { return wordWeights.Count; }
        }

        public override int Length
        {
            get { return wordWeights.Count; }
        }

        public override int Count(int idx)
        {
            return (int)wordWeights[idx] * wordWeights.Count;
        }

        public override int Word(int idx)
        {
            return idx;
        }

        public override string DocID
        {
            get;
            set;
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
            if (wordWeights != null)
            {
                BsonArray bsonWordCounts = new BsonArray();
                foreach (KeyValuePair<int, double> kvp in wordWeights)
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

        public LDAModel LoadFromDB(BsonDocument doc, DocModelDictionary wordDict)
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

            if (wordWeights != null)
            {
                wordWeights.Clear();
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
                            AddWord(wordKey, e.Value.AsDouble);
                            if (f) { f = false; } else { Debug.Assert(false); }
                        }
                    }
                }
            }

            if (wordWeights == null)
            {
                return null;
            }
            
            return this;
        }

        public void AddClassLabel(int classLabel)
        {
            if (classLabels == null)
            {
                classLabels = new HashSet<int>();
            }
            classLabels.Add(classLabel);
        }

        public void AddWord(int word, double weight)
        {
            if (wordWeights == null)
            {
                wordWeights = new Dictionary<int, double>();
            }            
            wordWeights.Add(word, weight);            
        }

        public void Init(double[] gamma)
        {
            double sum = 0;
            for (int i = 0; i < gamma.Length; i++)
            {
                if (gamma[i] > 1)
                {
                    sum += gamma[i] - 1;
                }                
            }

            for (int i = 0; i < gamma.Length; i++)
            {
                if (gamma[i] > 1)
                {
                    AddWord(i, gamma[i] - 1 / sum);
                }
                else
                {
                    AddWord(i, 0);
                }
            }
        }
    }
}
