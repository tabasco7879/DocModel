using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using System.IO;

namespace DocumentModel
{
    class LDABoWModel : BoWModel
    {
        double[] gamma;
        double[,] phi;
        LDABoWModelDB modelDB;        

        public static int max_iter = 20;
        public const double CONVERGENCE = 1e-6;

        public LDABoWModel(LDABoWModelDB db)
        {
            modelDB = db;
            Init();
        }

        public void Init()
        {
            if(gamma != null) gamma = null;
            gamma = new double[modelDB.NumOfTopics];
            if (phi != null) phi = null;            
        }

        public void WritePhi()
        {
            string dir = DocID.Substring(0, 3);
            string fileName = string.Format("{0}\\{1}", DocID.Substring(0, 3), DocID);
            int length = Buffer.ByteLength(phi);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            FileStream f = File.Create(fileName, length);
            byte[] array = new byte[length];
            Buffer.BlockCopy(phi, 0, array, 0, length);
            f.Write(array, 0, length);
            f.Close();
            phi = null;            
        }

        public void ReadPhi()
        {
            if (phi == null)
            {
                phi = new double[Length, modelDB.NumOfTopics];
            }
            string fileName = string.Format("{0}\\{1}", DocID.Substring(0, 3), DocID);
            if (File.Exists(fileName))
            {
                int length = Buffer.ByteLength(phi);
                byte[] array = new byte[length];
                FileStream f = File.OpenRead(fileName);                
                f.Read(array, 0, length);
                Buffer.BlockCopy(array, 0, phi, 0, length);
                f.Close();
            }
        }

        public double LDAPostInfer()
        {
            if (phi == null)
            {
                phi = new double[Length, modelDB.NumOfTopics];
            }

            double[] digamma_gam = new double[modelDB.NumOfTopics];

            for (int k = 0; k < modelDB.NumOfTopics; k++)
            {
                gamma[k] = modelDB.Alpha + (double)WordCount / modelDB.NumOfTopics;
                digamma_gam[k] = LDAUtil.digamma(gamma[k]);
                for (int n = 0; n < Length; n++)
                {
                    phi[n, k] = 1.0 / modelDB.NumOfTopics;
                }
            }

            double[] oldphi = new double[modelDB.NumOfTopics];

            int iter = 0;
            double likelihood_old = 0, likelihood = 0, converged = 1 + CONVERGENCE;
            while (iter < max_iter && converged > CONVERGENCE)
            {
                for (int n = 0; n < Length; n++)
                {
                    double phi_sum = 0;
                    for (int k = 0; k < modelDB.NumOfTopics; k++)
                    {
                        oldphi[k] = phi[n, k];
                        phi[n, k] = digamma_gam[k] + modelDB.Beta(Word(n), k);
                        if (k > 0)
                            phi_sum = LDAUtil.log_sum(phi_sum, phi[n, k]);
                        else
                            phi_sum = phi[n, k];
                    }

                    for (int k = 0; k < modelDB.NumOfTopics; k++)
                    {
                        phi[n, k] = Math.Exp(phi[n, k] - phi_sum); //normalize and exp
                        gamma[k] = gamma[k] + Count(n) * (phi[n, k] - oldphi[k]);
                        Debug.Assert(gamma[k] > 0);
                        digamma_gam[k] = LDAUtil.digamma(gamma[k]);
                    }
                }

                likelihood = LDALikelihood(gamma, phi);
                Debug.Assert(!double.IsNaN(likelihood));

                //converged = Math.Abs((likelihood - likelihood_old) / likelihood_old);
                converged = (likelihood_old - likelihood) / likelihood_old;
                if (likelihood < likelihood_old && iter > 0)
                {
                    Debug.Assert(true);
                }
                likelihood_old = likelihood;
                
                // different from LDA-C implementation, LDA-C seems to be stochastic update instead of batch update, but seems stochastic update is better on ap data
                //for (int k = 0; k < model.NumOfTopics; k++)
                //{                    
                //    digamma_gam[k] = LDAUtil.digamma(gamma[k]);
                //}
                iter++;
            }            

            return (likelihood_old); // likelihood may less than likelihood_old
        }

        public void UpdateSuffStat()
        {
            // prepare sufficient stats
            for (int n = 0; n < Length; n++)
            {
                for (int k = 0; k < modelDB.NumOfTopics; k++)
                {
                    double v = modelDB.ClassWord(Word(n), k) + Count(n) * phi[n, k];
                    modelDB.ClassWord(Word(n), k, v);
                    modelDB.ClassTotal[k] += Count(n) * phi[n, k];
                }
            }

            double gamma_sum = 0;
            for (int k = 0; k < modelDB.NumOfTopics; k++)
            {
                gamma_sum += gamma[k];
                modelDB.AlphaSuffStats += LDAUtil.digamma(gamma[k]);
                Debug.Assert(!double.IsNaN(modelDB.AlphaSuffStats));
            }
            modelDB.AlphaSuffStats -= modelDB.NumOfTopics * LDAUtil.digamma(gamma_sum);
            Debug.Assert(!double.IsNaN(modelDB.AlphaSuffStats));
        }

        public double LDALikelihood(double[] gamma, double[,] phi)
        {
            double likelihood = 0;

            double[] digamma_gam = new double[modelDB.NumOfTopics];

            double gamma_sum = 0;
            for (int k = 0; k < modelDB.NumOfTopics; k++)
            {
                digamma_gam[k] = LDAUtil.digamma(gamma[k]);
                gamma_sum += gamma[k];
            }
            double dig_sum = LDAUtil.digamma(gamma_sum);

            likelihood = modelDB.LogGammaAlphaSum - modelDB.SumLogGammaAlpha - LDAUtil.log_gamma(gamma_sum);

            for (int k = 0; k < modelDB.NumOfTopics; k++)
            {
                likelihood += (modelDB.Alpha - 1) * (digamma_gam[k] - dig_sum) + LDAUtil.log_gamma(gamma[k])
                    - (gamma[k] - 1) * (digamma_gam[k] - dig_sum);

                for (int n = 0; n < Length; n++)
                {
                    if (phi[n, k] > 0)
                    {
                        likelihood += Count(n) * (phi[n, k] * ((digamma_gam[k] - dig_sum) - Math.Log(phi[n, k])
                                        + modelDB.Beta(Word(n), k)));
                    }
                    else
                    {
                        Debug.Assert(phi[n, k] >= 0);
                    }
                }
            }

            return likelihood;
        }

        public BoWModel GetLDAModel()
        {
            BoWModel docModel = new BoWModel();
            docModel.DocID = DocID;
            for (int i = 0; i < Length; i++)
            {
                int ldaWord = LDAUtil.argmax(phi, i);
                docModel.AddWord(Word(i), ldaWord);
            }
            docModel.ClassLabels = ClassLabels;            
            return docModel;
        }
    }

    class LDABoWModelDB : BoWModelDB
    {
        /*
         * Dirichlet is a distribution of theta with parameter alpha and the configuation of alpha determines theta
         * the theta corresponding to bigger alpha is more likely be bigger, which means the asymmetry alpha results in high density area not in the middle of the simplex
         * but for LDA, alpha is not to determine which topic is significant in the corpus but to determine how many topics can be significant in a document, the smaller alpha, the less topics
         */
        double alpha; //different        
        double log_gamma_alpha_sum;
        double sum_log_gamma_alpha;
        Dictionary<int, double[]> beta; // in log form        
        double alpha_suffstats;
        Dictionary<int, double[]> class_word;
        double[] class_total;

        public const int MAX_ITER = 100;
        public const double CONVERGENCE = 1e-4;
        public const int NUM_INIT = 1;

        public LDABoWModelDB(int numOfTopics, WordDictionary wd)
            : base(wd)
        {
            alpha = 0.1;
            NumOfTopics = numOfTopics;
            //Init();
        }

        public void Init()
        {
            class_total = null;
            log_gamma_alpha_sum = 0;
            sum_log_gamma_alpha = 0;
            alpha_suffstats = 0;

            class_total = new double[NumOfTopics];
            if (beta == null)
            {
                beta = new Dictionary<int, double[]>();
            }
            else
            {
                beta.Clear();
            }
            foreach (KeyValuePair<string, int> kvp in wordDict.Dictionary)
            {
                beta.Add(kvp.Value, new double[NumOfTopics]);
            }

            if (class_word == null)
            {
                class_word = new Dictionary<int, double[]>();
            }
            else
            {
                class_word.Clear();
            }
            foreach (KeyValuePair<string, int> kvp in wordDict.Dictionary)
            {
                class_word.Add(kvp.Value, new double[NumOfTopics]);
            }

            for (int i = 0; i < docDB.Count; i++)
            {
                ((LDABoWModel)docDB[i]).Init();
            }
        }

        public int NumOfTopics
        {
            get;
            set;
        }

        public void LDAPostInferAlphaInit()
        {
            sum_log_gamma_alpha = NumOfTopics * LDAUtil.log_gamma(alpha);
            log_gamma_alpha_sum = LDAUtil.log_gamma(NumOfTopics * alpha);
        }

        public double Alpha
        {
            get { return alpha; }
        }

        public double LogGammaAlphaSum
        {
            get { return log_gamma_alpha_sum; }
        }

        public double SumLogGammaAlpha
        {
            get { return sum_log_gamma_alpha; }
        }

        public double[] ClassTotal
        {
            get { return class_total; }
        }

        public double AlphaSuffStats
        {
            get { return alpha_suffstats; }
            set { alpha_suffstats = value; }
        }

        public double Beta(int w, int k)
        {
            Debug.Assert(beta.ContainsKey(w) && k < NumOfTopics);
            return beta[w][k];
        }

        public double ClassWord(int w, int k)
        {
            Debug.Assert(class_word.ContainsKey(w) && k < NumOfTopics);
            return class_word[w][k];
        }

        public void ClassWord(int w, int k, double value)
        {
            Debug.Assert(class_word.ContainsKey(w) && k < NumOfTopics);
            class_word[w][k] = value;
        }

        public void ZeroSuffStats()
        {
            alpha_suffstats = 0;
            for (int i = 0; i < NumOfTopics; i++)
            {
                class_total[i] = 0;
            }

            foreach (KeyValuePair<int, double[]> kvp in class_word)
            {
                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    kvp.Value[i] = 0;
                }
            }
        }

        public void RunEM()
        {
            // init beta                        
            Corpus_Init();
            M_Step(false);
            // em
            double converged = 1 + CONVERGENCE;
            double likelihood_old = 0;
            int iter = 0;
            while (iter < MAX_ITER && converged > CONVERGENCE)
            {
                ZeroSuffStats();
                double likelihood = E_Step();
                
                M_Step();

                converged = Math.Abs((likelihood - likelihood_old) / likelihood_old);
                string printline = string.Format("iter{0} likelihood:{1}", iter, likelihood);                
                if (likelihood < likelihood_old && iter > 0)
                {
                    printline += " diverged";
                    //LDABoWModel.max_iter = LDABoWModel.max_iter * 2;
                    Debug.Assert(true);
                }
                Console.WriteLine(printline);
                
                likelihood_old = likelihood;
                iter++;                
            }            
            E_Step();
        }

        public double E_Step()
        {
            LDAPostInferAlphaInit();
            double likelihood = 0;
            //StreamWriter sw = new StreamWriter(new FileStream("likelihood.test", FileMode.Create));
            for (int i = 0; i < Count; i++)
            {
                //((LDABoWModel)this[i]).ReadPhi();
                double d_likelihood = ((LDABoWModel)this[i]).LDAPostInfer();
                ((LDABoWModel)this[i]).UpdateSuffStat();
                //((LDABoWModel)this[i]).WritePhi();
                //sw.WriteLine("{0}", d_likelihood);
                likelihood += d_likelihood;
            }
            //sw.Close();
            Debug.Assert(likelihood != 0);

            return likelihood;
        }

        public void M_Step()
        {
            M_Step(true);
        }

        public void M_Step(bool updateAlpha)
        {
            // update beta
            for (int k = 0; k < NumOfTopics; k++)
            {
                foreach (KeyValuePair<string, int> w in wordDict.Dictionary)
                {
                    if (class_word[w.Value][k] > 0)
                        beta[w.Value][k] = Math.Log(class_word[w.Value][k]) - Math.Log(class_total[k]);
                    else
                        beta[w.Value][k] = -100;
                }
            }

            //StreamWriter sw = new StreamWriter(new FileStream("beta.test", FileMode.Create));
            //for (int k = 0; k < NumOfTopics; k++)
            //{
            //    for (int w = 0; w < wordDict.Dictionary.Count; w++)
            //    {
            //        sw.WriteLine("{0}", beta[w][k]);
            //    }
            //}
            //sw.Close();

            // update alpha            
            if (updateAlpha)
            {
                alpha = LDAUtil.opt_alpha(alpha_suffstats, Count, NumOfTopics);
                Console.WriteLine("new alpha = {0}", alpha);
            }
        }

        public void Corpus_Init()
        {
            int num_init = NUM_INIT;
            //num_init = 1; //TODO: remove after test
            Random rand = new Random();
            for (int k = 0; k < NumOfTopics; k++)
            {
                for (int i = 0; i < num_init; i++)
                {
                    //int doc_idx = k; //TODO: remove after test 
                    int doc_idx = (int)(rand.NextDouble() * Count); 
                    for (int n = 0; n < this[doc_idx].Length; n++)
                    {
                        class_word[this[doc_idx].Word(n)][k] += this[doc_idx].Count(n);
                    }
                }
                foreach (KeyValuePair<string, int> w in wordDict.Dictionary)
                {
                    class_word[w.Value][k] += 1.0;
                    class_total[k] += class_word[w.Value][k];
                }
            }
        }

        public override DocModel LoadFromDB(BsonDocument bsonDoc)
        {
            LDABoWModel docModel = new LDABoWModel(this);
            return docModel.LoadFromDB(bsonDoc, wordDict);
        }

        public void TopBeta()
        {
            StreamWriter sw = new StreamWriter(new FileStream("top20words.test", FileMode.Create));                        
            List<KeyValuePair<int, double[]>> sortList = beta.ToList<KeyValuePair<int, double[]>>();
            for (int i = 0; i < NumOfTopics; i++)
            {
                sortList.Sort(
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
                sw.WriteLine("topic {0}", i);
                for (int j = 0; j < 40; j++)
                {
                    sw.WriteLine(wordDict.GetKey(sortList[j].Key));
                }
            }
            sw.Close();
        }

        public void SaveModel(int classKey)
        {
            StreamWriter writer = new StreamWriter(new FileStream("model\\ldamodel_" + classKey + "_" + NumOfTopics+".beta", FileMode.Create));
            foreach (KeyValuePair<int, double[]> kvp in beta)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(kvp.Key);
                sb.Append(':');
                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    sb.Append(kvp.Value[i]);
                    sb.Append(',');
                }
                sb.Remove(sb.Length - 1, 1);
                writer.WriteLine(sb.ToString());
            }
            writer.Close();

            writer = new StreamWriter(new FileStream("model\\ldamodel_" + classKey + "_" + NumOfTopics + ".alpha", FileMode.Create));
            writer.WriteLine(alpha);
            writer.Close();
        }

        public void StoreLDA(int classKey, string type)
        {
            MongoCollection<BsonDocument> ldamodel = db.GetCollection<BsonDocument>("ldamodel_"+ type +"_" + classKey + "_" + NumOfTopics);
            ldamodel.RemoveAll();            
            for (int i = 0; i < docDB.Count; i++)
            {
                BoWModel docModel = ((LDABoWModel)this[i]).GetLDAModel();
                ldamodel.Insert(docModel.StoreToDB());
            }
        }

    }
}
