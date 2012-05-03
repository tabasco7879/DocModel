using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentModel;

namespace PrecedenceModel
{
    class PrecedenceModel
    {
        public PrecedenceModel(DocModelDictionary dictionary)
        {
            this.dictionary = dictionary;
        }

        public void LoadInstances()
        {
            if (dbs == null)
                dbs = new List<PrecedenceInstanceDB>();
            else
                dbs.Clear();
            
            int[] listOfTopicNumbers = { 100, 80, 50, 30, 20, 10 };
            for (int i = 0; i < listOfTopicNumbers.Length; i++)
            {
                PrecedenceInstanceDB db = new PrecedenceInstanceDB(dictionary);
                db.LoadInstances("ldadocs_" + listOfTopicNumbers);
            }
        }

        public void DiscoverPrecedence(PrecedenceInstanceDB db)
        {
            for (int i = 0; i < db.Count; i++)
            {

            }
        }

        List<PrecedenceInstanceDB> dbs;
        DocModelDictionary dictionary;

    }
}
