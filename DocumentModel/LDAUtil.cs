using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocumentModel
{
    class LDAUtil
    {
        /*
         * given log(a) and log(b), return log(a + b)
         *
         */
        public static double log_sum(double log_a, double log_b)
        {
            double v;

            if (log_a < log_b)
            {
                v = log_b + Math.Log(1 + Math.Exp(log_a - log_b));
            }
            else
            {
                v = log_a + Math.Log(1 + Math.Exp(log_b - log_a));
            }

            return (v);
        }

        /**
        * Proc to calculate the value of the trigamma, the second
        * derivative of the loggamma function. Accepts positive matrices.
        * From Abromowitz and Stegun.  Uses formulas 6.4.11 and 6.4.12 with
        * recurrence formula 6.4.6.  Each requires workspace at least 5
        * times the size of X.
        *
        **/
        public static double trigamma(double x)
        {
            double p;
            int i;

            x = x + 6;
            p = 1 / (x * x);
            p = (((((0.075757575757576 * p - 0.033333333333333) * p + 0.0238095238095238)
                 * p - 0.033333333333333) * p + 0.166666666666667) * p + 1) / x + 0.5 * p;
            for (i = 0; i < 6; i++)
            {
                x = x - 1;
                p = 1 / (x * x) + p;
            }
            return (p);
        }

        /*
         * taylor approximation of first derivative of the log gamma function
         *
         */
        public static double digamma(double x)
        {
            double p;
            x = x + 6;
            p = 1 / (x * x);
            p = (((0.004166666666667 * p - 0.003968253986254) * p +
            0.008333333333333) * p - 0.083333333333333) * p;
            p = p + Math.Log(x) - 0.5 / x - 1 / (x - 1) - 1 / (x - 2) - 1 / (x - 3) - 1 / (x - 4) - 1 / (x - 5) - 1 / (x - 6);
            return p;
        }


        public static double log_gamma(double x)
        {
            double z = 1 / (x * x);

            x = x + 6;
            z = (((-0.000595238095238 * z + 0.000793650793651)
            * z - 0.002777777777778) * z + 0.083333333333333) / x;
            z = (x - 0.5) * Math.Log(x) - x + 0.918938533204673 + z - Math.Log(x - 1) -
            Math.Log(x - 2) - Math.Log(x - 3) - Math.Log(x - 4) - Math.Log(x - 5) - Math.Log(x - 6);
            return z;
        }


        /*
         * argmax
         *
         */
        public static int argmax(double[,] x, int idx)
        {
            int i;
            double max = x[idx, 0];
            int argmax = 0;
            for (i = 1; i < x.GetLength(1); i++)
            {
                if (x[idx, i] > max)
                {
                    max = x[idx, i];
                    argmax = i;
                }
            }
            return (argmax);
        }

        public static double alhood(double a, double ss, int D, int K)
        { return (D * (log_gamma(K * a) - K * log_gamma(a)) + (a - 1) * ss); }

        public static double d_alhood(double a, double ss, int D, int K)
        { return (D * (K * digamma(K * a) - K * digamma(a)) + ss); }

        public static double d2_alhood(double a, int D, int K)
        { return (D * (K * K * trigamma(K * a) - K * trigamma(a))); }


        /*
         * newtons method
         *
         */
        public static double opt_alpha(double ss, int D, int K)
        {
            double a, log_a, init_a = 100;
            double f, df, d2f;
            int iter = 0;

            log_a = Math.Log(init_a);
            do
            {
                iter++;
                a = Math.Exp(log_a);
                if (double.IsNaN(a))
                {
                    init_a = init_a * 10;
                    Console.WriteLine("warning : alpha is nan; new init = {0:0.00000}", init_a);
                    a = init_a;
                    log_a = Math.Log(a);
                }
                f = alhood(a, ss, D, K);
                df = d_alhood(a, ss, D, K);
                d2f = d2_alhood(a, D, K);
                log_a = log_a - df / (d2f * a + df);
                //Console.WriteLine("alpha maximization : {0:0.00000} {1:0.00000}", f, df);
            }while ((Math.Abs(df) > NEWTON_THRESH) && (iter < MAX_ALPHA_ITER));
            return (Math.Exp(log_a));
        }

        public const double NEWTON_THRESH = 1e-5;
        public const int MAX_ALPHA_ITER = 1000;

    }
}
