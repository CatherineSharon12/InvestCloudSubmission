using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MatrixMultiplier
{
    public class ResponseModel
    {
        public string Value { get; set; }      
        public string Cause { get; set; }
        public bool Success { get; set; }
    }

    public class MatrixResponse {
        public int[] Value { get; set; }
        public string Cause { get; set; }
        public bool Success { get; set; }
    }

    class Program
    {
        static HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            try
            {
                RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}",e.Message);
            }
        }

        /// <summary>
        /// Main implementation
        /// </summary>
        /// <returns></returns>
        static async Task RunAsync()
        {
            int size = 0;
            //Ensure that the size of the matrix is within the limit
            do
            {
                Console.WriteLine("Enter the size of the matrices. Value must be between 1 and 1000");
                size = Convert.ToInt32(Console.ReadLine());
            } while (!Enumerable.Range(2, 1000).Contains(size));
            
            client.BaseAddress = new Uri("https://recruitment-test.investcloud.com");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //HTTP Get to initialize the matrices
            ResponseModel resp = await InitializeMatricesAsync(size);
            if (!resp.Success)
            {
                Console.WriteLine("Request Failed");
            }
            int[][] A = null;
            int[][] B = null;
            int[][] result = null;

            Console.WriteLine("Initializing Matrices...");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            //Initialize the matrices locally
            A = CreateMatrix(size);
            B = CreateMatrix(size);

            Task<int[][]> GetA = PopulateMatrixAsync(size, "A");
            Task<int[][]> GetB = PopulateMatrixAsync(size, "B");

            Task.WaitAll(GetA, GetB);
            A = GetA.Result;
            B = GetB.Result;

            Console.WriteLine("Initialized Matrices!\nComputing Product..");
            
            result = MultiplyMatrices(A,B);
            List<int[]> resultList = result.ToList();
            StringBuilder tempString = new StringBuilder();
            foreach (int[] array in resultList)
            {
                tempString.Append(String.Join("", array));
            }

            string hashedString = GetMD5HashedString(tempString.ToString());
            Console.WriteLine("Hashed string: {0}", hashedString);
            resp = await ValidateResult(hashedString);
            Console.WriteLine("Response: {0}\nStatus:{1}", resp.Value, resp.Success ? "Success" : "Fail");
            sw.Stop();
            TimeSpan ts = sw.Elapsed;
            string computationTime = String.Format("{0:00} minutes and {1:00}.{2:00} seconds", ts.Minutes, ts.Seconds,ts.Milliseconds / 10);
            Console.WriteLine("Completed in {0}", computationTime);
            Console.ReadLine();

 
        }

        /// <summary>
        ///  API call to initialize the matrices
        /// </summary>
        /// <param name="size">size of the matrix</param>
        /// <returns>response object that indicates whether the server was accessible</returns>
        static async Task<ResponseModel> InitializeMatricesAsync(int size)
        {           
            string path = string.Format("https://recruitment-test.investcloud.com/api/numbers/init/{0}", size.ToString());
            HttpResponseMessage response = await client.GetAsync(path);
            string value = string.Empty;
            if (response.IsSuccessStatusCode)
            {
                value = await response.Content.ReadAsStringAsync();
            }

            ResponseModel result = JsonConvert.DeserializeObject<ResponseModel>(value);
            return result;
        }

        /// <summary>
        /// API calls to fetch the rows of the specified matric asynchronously
        /// </summary>
        /// <param name="size"> size of the matrix</param>
        /// <param name="dataSet">name of the dataset being populated</param>
        /// <returns>populated matrix</returns>
        static async Task<int[][]> PopulateMatrixAsync(int size, string dataSet)
        {
            int[][] fetchedArray = CreateMatrix(size);
            string row = "row";
            for (int i = 0; i < size; i++)
            {
                string path = string.Format("https://recruitment-test.investcloud.com/api/numbers/{0}/{1}/{2}", dataSet, row, i.ToString());
                HttpResponseMessage response = await client.GetAsync(path);
                string value = string.Empty;
                if (response.IsSuccessStatusCode)
                {
                    value = await response.Content.ReadAsStringAsync();
                }
                MatrixResponse result = JsonConvert.DeserializeObject<MatrixResponse>(value);
                fetchedArray[i] = result.Value;
            }
            return fetchedArray;
        }

        static int[][] MultiplyMatrices(int[][] A, int[][] B)
        {            
            int size = A.Length;
            int[][] product = CreateMatrix(size);
            Parallel.For(0, size, i => {
                for (int j = 0; j < size; j++)
                {
                    for (int k = 0; k < size; k++)
                    {
                        product[i][j] += A[i][k] * B[k][j];
                    }
                }
            });

            return product;
        }

        /// <summary>
        /// Initializes the matrix with 0's and returns it
        /// </summary>
        /// <param name="size"></param>
        /// <returns>0 initialized matrix</returns>
        static int[][] CreateMatrix(int size)
        {
            int[][] matrix = new int[size][];
            for (int i = 0; i < size; i++)
                matrix[i] = new int[size];
            return matrix;
        }

        /// <summary>
        /// Computes the MD5 hash of the given string
        /// </summary>
        /// <param name="inputString">string that has to be hashed</param>
        /// <returns>hashed string</returns>
        static string GetMD5HashedString(string inputString)
        {
            //using (var md5 = MD5.Create())
            //        return GetMd5Hash(md5.ComputeHash(Encoding.UTF8.GetBytes(inputString)));
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(inputString);
            byte[] hash = md5.ComputeHash(inputBytes);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// HTTP Post to the Web API in order to validate the result
        /// </summary>
        /// <param name="hashedString"></param>
        /// <returns></returns>
        static async Task<ResponseModel> ValidateResult(string hashedString)
        {
            string output = string.Empty;
            string path = "https://recruitment-test.investcloud.com/api/numbers/validate";
            HttpResponseMessage response = client.PostAsJsonAsync(path, hashedString).Result;
            string value = string.Empty;
            if (response.IsSuccessStatusCode)
            {
                value = await response.Content.ReadAsStringAsync();
            }
            ResponseModel result = JsonConvert.DeserializeObject<ResponseModel>(value);
            return result;
        }
    }
}
