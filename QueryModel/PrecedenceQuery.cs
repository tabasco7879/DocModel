using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PModel = PrecedenceModel;
using DocumentModel;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace QueryModel
{
    class PrecedenceQuery
    {
        public PrecedenceQuery(PModel.PrecedenceModel pModel, DocModelDictionary wordDictionary, DocModelDictionary classLabelDictionary)
        {
            this.pModel = pModel;
            this.wordDictionary = wordDictionary;
            this.classLabelDictionary = classLabelDictionary;
        }

        public bool IsSatisfied(DocModel instance, PModel.PrecedenceProperty p1, PModel.InstanceDB pDB, bool usePQuery)
        {
            if (usePQuery)
            {
                LDAModel ldaDoc = (LDAModel)pDB[pDB.SearchByDocID(instance.DocID)];
                if (ldaDoc != null)
                {
                    for (int i = 0; i < pModel.PrecedenceRelations.Count; i++)
                    {
                        PModel.PrecedenceRelation relation = pModel.PrecedenceRelations[i];
                        if (relation.P1 == p1)
                        {
                            int[] p = relation.P2.Values;
                            bool isSatisfied = true;
                            foreach (int v in p)
                            {
                                if (!ldaDoc.WordList.Contains(v))
                                {
                                    isSatisfied = false;
                                    break;
                                }
                            }
                            if (isSatisfied)
                                return isSatisfied;
                        }
                    }
                }
            }

            for (int i = 0; i < p1.Values.Length; i++)
            {
                string s = classLabelDictionary.GetKey(p1.Values[i]);
                List<int> queryWords = new List<int>();
                string l = s.ToLower();
                Match m;
                Regex r = new Regex(@"[a-zA-Z]+[0-9]*");
                for (m = r.Match(l); m.Success; m = m.NextMatch())
                {
                    int word = -1;
                    wordDictionary.Dictionary.TryGetValue(Detachment.Instance.Detach(m.Value), out word);
                    if (word >= 0)
                    {
                        queryWords.Add(word);
                    }
                    else
                    {
                        Debug.Assert(1 == 1);
                    }
                }
                for (int j = 0; j < queryWords.Count; j++)
                {
                    if (instance[queryWords[j]] <= 0)
                    {
                        return false;
                    }
                }                
            }

            return true;
        }

        bool IsSatisfied(LDAModel ldaDoc, PModel.PrecedenceProperty p2)
        {
            return true;
        }

        public void Validate(DocModel instance, PModel.PrecedenceProperty p1, bool isSatisfied,ref int hit, ref int miss, ref int bad)
        {
            int[] p = p1.Values;
            foreach (int v in p)
            {
                if (!instance.HasClassLabel(v))
                {
                    if (isSatisfied)
                    {
                        bad++;
                        return;
                    }
                    else
                    {
                        return;
                    }
                }                
            }

            if (isSatisfied)
                hit++;
            else
                miss++;
        }

        public void TestQuery(DocModelDB db)
        {
            PModel.InstanceDB.collectionName = "ldadocs_500_all";
            PModel.InstanceDB pDB = new PModel.InstanceDB();
            pDB.LoadInstances();

            HashSet<PModel.PrecedenceProperty> testingQueries = new HashSet<PModel.PrecedenceProperty>();
            for (int i = 0; i < pModel.PrecedenceRelations.Count; i++)
            {
                PModel.PrecedenceRelation relation = pModel.PrecedenceRelations[i];
                testingQueries.Add(relation.P1);
            }

            int hit = 0, miss = 0, bad = 0;
            foreach (PModel.PrecedenceProperty q in testingQueries)
            {
                for (int i = 0; i < db.Count; i++)
                {
                    bool result = IsSatisfied(db[i], q, pDB, true);
                    Validate(db[i], q, result, ref hit, ref miss, ref bad);
                }
            }
        }

        PModel.PrecedenceModel pModel;        
        DocModelDictionary wordDictionary;
        DocModelDictionary classLabelDictionary;
       
    }
}
