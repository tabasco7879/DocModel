using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentModel;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace PrecedenceModel
{
    class PrecedenceInstanceDB : DocModelDB
    {

        public PrecedenceInstanceDB(DocModelDictionary wd) : base(wd)
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

        public void LoadInstances(string collectionName)
        {
            this.collectionName = collectionName;
            LoadFromDB();
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

        string collectionName;
    }
}
