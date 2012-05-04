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
        List<int> wordList;

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
            return 0;
        }

        public List<int> WordList
        {
            get
            {
                if (wordList == null)
                {                    
                    //wordList = wordWeights.Keys.ToList();
                    wordList = new List<int>();
                    List<KeyValuePair<int, double>> weights = wordWeights.ToList();
                    weights.Sort((x1, x2) =>
                        {
                            if (x1.Value > x2.Value)
                                return -1;
                            else if (x1.Value == x2.Value)
                                return 0;
                            else
                                return 1;
                        }
                    );
                    for (int i = 0; i < weights.Count; i++)
                    {
                        wordList.Add(weights[i].Key);
                        if (i + 1 > weights.Count * 0.1)
                            break;
                    }
                    wordList.Sort();
                }
                return wordList;
            }
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
                        AddWord(wordKey, e.Value.AsDouble);
                        if (f) { f = false; } else { Debug.Assert(false); }                        
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
                    AddWord(i, (gamma[i] - 1) / sum);
                }
            }
        }
    }
}
