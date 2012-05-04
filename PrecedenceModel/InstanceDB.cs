using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentModel;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using System.IO;
using System.Diagnostics;

namespace PrecedenceModel
{
    class InstanceDB : DocModelDB
    {

        public InstanceDB() : base(null)
        {               
        }

        public override string DBName
        {
            get { return "docmodel"; }
        }

        public override string CollectionName
        {
            get { return collectionName; }            
        }

        public Dictionary<int, string[]> Vocabulary
        {
            get { return vocabulary; }
        }

        public void LoadInstances()
        {                     
            LoadFromDB();
            BuildIndex();
        }

        public int SearchByDocID(string docId)
        {
            int idx = -1;
            if (docIDMap != null)
                docIDMap.TryGetValue(docId, out idx);
            return idx;
        }

        public override DocModel LoadFromDB(BsonDocument bsonDoc)
        {
            LDAModel ldaDoc = new LDAModel();
            return ldaDoc.LoadFromDB(bsonDoc, wordDict);   
        }

        public override void Stats(DocModelDictionary classLabelDict)
        {
            throw new NotImplementedException();
        }

        public void LoadLDAModel(string modelName, int numOfTopics, DocModelDictionary dictionary)
        {
            FileStream fstream = new FileStream(modelName, FileMode.Open);
            StreamReader reader = new StreamReader(fstream);
            List<KeyValuePair<int, double[]>> beta = new List<KeyValuePair<int, double[]>>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] ss = line.Split(':');
                int wordKey = int.Parse(ss[0]);
                double[] betaWord = new double[numOfTopics];
                string[] sss = ss[1].Split(',');
                Debug.Assert(sss.Length == numOfTopics);
                for (int i = 0; i < sss.Length; i++)
                {
                    betaWord[i] = double.Parse(sss[i]);
                }
                beta.Add(new KeyValuePair<int, double[]>(wordKey, betaWord));
            }
            reader.Close();

            if (vocabulary == null)
            {
                vocabulary = new Dictionary<int, string[]>();
            }
            else
            {
                vocabulary.Clear();
            }

            for (int i = 0; i < numOfTopics; i++)
            {
                beta.Sort(
                    (x1, x2) =>
                    {
                        if (x1.Value[i] > x2.Value[i])
                        {
                            return -1;
                        }
                        else if (x1.Value[i] == x2.Value[i])
                        {
                            return 0;
                        }
                        return 1;
                    }
                    );
                string[] wordList = new string[40];
                for (int j = 0; j < 40; j++)
                {
                    wordList[j] = dictionary.GetKey(beta[j].Key);
                }
                vocabulary.Add(i, wordList);                    
            }
        }

        protected void BuildIndex()
        {
            if (docIDMap == null)
            {
                docIDMap = new Dictionary<string, int>();
            }
            else
            {
                docIDMap.Clear();
            }

            for (int i = 0; i < Count; i++)
            {
                docIDMap.Add(this[i].DocID, i);
            }
        }

        Dictionary<int, string[]> vocabulary;
        public static string collectionName;
        Dictionary<string, int> docIDMap;
    }
}
