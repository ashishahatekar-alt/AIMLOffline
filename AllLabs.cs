using Microsoft.Agents.AI;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.Data;

namespace AIMLOffline
{
    public class AllLabs
    {
        public static void Lab1_TalkingwithLLM()
        {
            string apiKey = Environment.GetEnvironmentVariable("aikey") ?? "your-key-here";
            string modelId = "gpt-4o";

            IChatClient chatClient = new ChatClient(modelId, apiKey).AsIChatClient();
            AIAgent agent = chatClient.AsAIAgent(
                name: "MockInterviewAgent",
                instructions: "You are the travel agent. Give me 3 places to visit based on city entered by user "
            );
            Console.Write("City Name? ");
            string userPrompt = Console.ReadLine();
            Console.WriteLine($"User: {userPrompt}\n");

            var response = agent.RunAsync(userPrompt).Result;
            Console.WriteLine($"Agent: {response.Text}");

            Console.ReadLine();
        }

        public static async Task Lab2_3SmallEmbedding()
        {
            var key = Environment.GetEnvironmentVariable("aikey");
            var embeddingClient = new EmbeddingClient(
                model: "text-embedding-3-small",
                apiKey: key
            );

            string textToBeMatched = "junior developer 0 to 2 years";
            var embed = await embeddingClient.GenerateEmbeddingAsync(textToBeMatched);
            var textToBeMatchedVector = embed.Value.ToFloats().ToArray();
            while (true)
            {
                Console.WriteLine();
                Console.Write("Enter text to be matched (type 'exit' to quit): ");
                string? inputText = Console.ReadLine();
                if (inputText?.ToLower() == "exit")
                {
                    break;
                }
                var embedinput = await embeddingClient.GenerateEmbeddingAsync(inputText);
                var inputTextVector = embedinput.Value.ToFloats().ToArray();
                double similarity = CalculateCosineSimilarity(
                    textToBeMatchedVector,
                    inputTextVector);
                Console.WriteLine($"Cosine Similarity : {similarity:F4}");
            }
        }

        public static double CalculateCosineSimilarity(float[] vector1, float[] vector2)

        {
            if (vector1.Length != vector2.Length)
            {
                throw new ArgumentException("Vectors must be of the same length.");
            }

            double dotProduct = 0.0;
            double magnitude1 = 0.0;
            double magnitude2 = 0.0;

            for (int i = 0; i < vector1.Length; i++)

            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];

            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);
            if (magnitude1 == 0.0 || magnitude2 == 0.0)

            {
                // Handle cases where one or both vectors are zero vectors
                return 0.0;
            }
            return dotProduct / (magnitude1 * magnitude2);
        }

        public class RAGLookup
        {
            public int Id { get; set; }
            public string Description { get; set; }
            public string QuestionstobeAsked { get; set; }
            public float[] DescriptionEmbedding { get; set; }
            public List<String> Questions { get; set; }
            public RAGLookup()
            {
                Questions = new List<String>();
            }
        }

        public static List<RAGLookup> GetCurrentExperience()
        {
            var list = new List<RAGLookup>();

            using (SqlConnection con = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=InterviewQuestions;Integrated Security=True;Trusted_Connection=True;"))
            {
                string query = @"
                  SELECT Id, Expierience, Questions
                  FROM dbo.tblExp1";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            list.Add(new RAGLookup
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                Description = dr["Expierience"].ToString(),
                                QuestionstobeAsked = dr["Questions"]?.ToString()
                            });
                        }
                    }
                }
            }

            return list;
        }

        public static void UpdateEmbeddingFortheExperience(RAGLookup item)
        {
            string vectorValue = "[" + string.Join(",", item.DescriptionEmbedding) + "]";

            using (SqlConnection con = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=InterviewQuestions;Integrated Security=True;Trusted_Connection=True;"))
            {
                string query = @"
                 UPDATE dbo.tblExp1
                 SET ExpVector = CAST(@ExpVector AS vector(1536))
                 WHERE Id = @Id";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = item.Id;
                    cmd.Parameters.Add("@ExpVector", SqlDbType.NVarChar, -1).Value = vectorValue;

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void InsertQuestionForThatExperience(int expId, string question)
        {
            using (SqlConnection con = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=InterviewQuestions;Integrated Security=True;Trusted_Connection=True;"))
            {
                string query = @"
                 INSERT INTO dbo.tblQuestions (ExpId, Question)
                 VALUES (@ExpId, @Question)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@ExpId", SqlDbType.Int).Value = expId;
                    cmd.Parameters.Add("@Question", SqlDbType.VarChar, 1000).Value = question;

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static async Task Lab3_MicrosoftAgentWithRag()
        {
            string apiKey = Environment.GetEnvironmentVariable("aikey") ?? "your-key-here";
            string modelId = "gpt-4o";
            var embeddingClient = new EmbeddingClient("text-embedding-3-small", apiKey);

            Console.Write("Enter who you are ?");
            string userPrompt = Console.ReadLine();
            Console.WriteLine($"User: {userPrompt}\n");
            IChatClient chatClient = new ChatClient(modelId, apiKey).AsIChatClient();
            var candidateEmbed = await embeddingClient.GenerateEmbeddingAsync(userPrompt);
            var candidateVector = candidateEmbed.Value.ToFloats().ToArray();

            var bestMatchContext = GetBestTopMatch(candidateVector);

            Console.WriteLine(bestMatchContext.Description);
            AIAgent agent = chatClient.AsAIAgent(
                name: "MockInterviewAgent",
                instructions: "Send only 3 interview questions as per the " +
                             " as per the skill put by the developer "
            //tools : [AIFunctionFactory.Create(EmailTool.SendEmail)]

            );


            var response = agent.RunAsync(userPrompt + " ask questions around " + bestMatchContext.QuestionstobeAsked).Result;
            Console.WriteLine($"Agent: {response.Text}");

            Console.ReadLine();
        }

        public static void BuildSQLServerRAG()
        {
            var key = Environment.GetEnvironmentVariable("aikey");
            var embeddingClient = new EmbeddingClient("text-embedding-3-small", key);
            var chat = new ChatClient(model: "gpt-4o-mini", key);
            List<RAGLookup> lookupStore = GetCurrentExperience(); // GetCurrentExperience()

            foreach (var item in lookupStore)
            {
                var embed = embeddingClient.GenerateEmbeddingAsync(item.Description).Result;

                item.DescriptionEmbedding = embed.Value.ToFloats().ToArray();
                UpdateEmbeddingFortheExperience(item);
                // take all question and store in DB for now DB is inmemory
                var messages = new List<OpenAI.Chat.ChatMessage>
                 {
                 new SystemChatMessage(
                 $"You are a technical interviewer. Based on this experience: '{item.Description} ' , " +
                 "generate exactly 10 distinct, non-overlapping interview questions covering + " + item.QuestionstobeAsked  +
                 ".Return ONLY the questions, each on a fresh new line starting with its number (e.g., '1. ', '2. '). " +
                 "Do NOT include introductory remarks, markdown formatting, greetings, or conversational filler text.")
                 };

                Console.WriteLine("Open AI Calling and loading questions for " + item.Description);

                var completion = chat.CompleteChatAsync(messages).Result;
                string fullResponse = completion.Value.Content.Last().Text;

                ChatTokenUsage usage = completion.Value.Usage;
                Console.WriteLine($"Input (Prompt) Tokens: {usage.InputTokenCount}");
                Console.WriteLine($"Output (Completion) Tokens: {usage.OutputTokenCount}");
                Console.WriteLine($"Total Tokens Consumed: {usage.TotalTokenCount}");

                string[] interviewQuestions = fullResponse.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var question in interviewQuestions)
                {
                    InsertQuestionForThatExperience(item.Id, question);
                }
            }
        }

        public static RAGLookup GetBestTopMatch(float[] queryVector)
        {
            string vectorValue = "[" + string.Join(",", queryVector) + "]";

            RAGLookup result = null;

            using (SqlConnection con = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=InterviewQuestions;Integrated Security=True;Trusted_Connection=True;"))
            {
                con.Open();

                string sql = @"
                SELECT TOP (1)
                Id,
                Expierience,
                Questions,
                VECTOR_DISTANCE(
                    'cosine',
                    ExpVector,
                    CAST(@QueryVector AS vector(1536))
                ) AS Score
                FROM dbo.tblExp1
                WHERE ExpVector IS NOT NULL
                ORDER BY VECTOR_DISTANCE(
                    'cosine',
                    ExpVector,
                    CAST(@QueryVector AS vector(1536)));";

                using (SqlCommand cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.Add("@QueryVector", SqlDbType.NVarChar, -1).Value = vectorValue;

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            result = new RAGLookup
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                Description = dr["Expierience"].ToString(),
                                QuestionstobeAsked = dr["Questions"]?.ToString(),
                                DescriptionEmbedding = queryVector
                            };
                        }
                    }
                }

                if (result != null)
                {
                    string questionSql = @"
                    SELECT Question
                    FROM dbo.tblQuestions
                    WHERE ExpId = @ExpId
                    ORDER BY Id;";

                    using (SqlCommand cmd = new SqlCommand(questionSql, con))
                    {
                        cmd.Parameters.Add("@ExpId", SqlDbType.Int).Value = result.Id;

                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                result.Questions.Add(dr["Question"].ToString());
                            }
                        }
                    }
                }
            }

            return result;
        }

        public static void Lab4_ToolCalling()
        {
            string apiKey = Environment.GetEnvironmentVariable("aikey") ?? "your-key-here";
            string modelId = "gpt-4o";

            //IChatClient chatClient = new ChatClient(modelId, apiKey).AsIChatClient();

            IChatClient chatClient = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");
            // AddScoped<IchatClient,ChatClient>
            // Context Safety...
            // If the context safe...
            AIAgent agent = chatClient.AsAIAgent(
                name: "MockInterviewAgent",
                instructions: "Send only 3 interview questions on object oriented programming as per the " +
                             " as per the skill put by the developer " +
                             " if he enter email call the sendemail tool ",
            // + " but ask questions only on OOP and SQL basic"
            tools: [AIFunctionFactory.Create(EmailTool.SendEmail)]

            );
            Console.Write("Enter who you are  and enter your email?");
            string userPrompt = Console.ReadLine();

            Console.WriteLine($"User: {userPrompt}\n");

            var response = agent.RunAsync(userPrompt).Result;
            Console.WriteLine($"Agent: {response.Text}");

            Console.ReadLine();
        }

    }
}
