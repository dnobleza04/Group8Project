using System;
using System.Collections.Generic;
using System.Web.Services;
using System.Web.Script.Services;
using MySql.Data.MySqlClient;

namespace ProjectTemplate
{
	[WebService(Namespace = "http://tempuri.org/")]
	[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
	[System.ComponentModel.ToolboxItem(false)]
	[ScriptService]
	public class ProjectServices : WebService
	{
		private string dbID = "cis440Spring2026team8";
		private string dbPass = "cis440Spring2026team8";
		private string dbName = "cis440Spring2026team8";

		private string getConString()
		{
			return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName + "; UID=" + dbID + "; PASSWORD=" + dbPass;
		}

		public class LoginResult
		{
			public bool success { get; set; }
			public string message { get; set; }
		}

		public class BasicResult
		{
			public bool success { get; set; }
			public string message { get; set; }
		}

		public class AccessStatusResult
		{
			public bool loggedIn { get; set; }
			public bool verified { get; set; }
			public string message { get; set; }
		}

		public class SuggestionRow
		{
			public int SuggestionID { get; set; }
			public string SuggestionText { get; set; }
			public string CreatedDate { get; set; }
			public int UpVotes { get; set; }
			public int DownVotes { get; set; }
		}

		public class VoteQuestionRow
		{
			public int QuestionID { get; set; }
			public string QuestionText { get; set; }
			public string CreatedDate { get; set; }
			public int RightVotes { get; set; }
			public int LeftVotes { get; set; }
		}

		public class VoteQuestionResult
		{
			public bool success { get; set; }
			public string message { get; set; }
			public bool hasNext { get; set; }
			public VoteQuestionRow nextQuestion { get; set; }
		}

		private string EnsureAnonId()
		{
			if (Session["anonId"] == null)
			{
				Session["anonId"] = Guid.NewGuid().ToString();
			}
			return Session["anonId"].ToString();
		}

		private bool IsLoggedInSession()
		{
			return Session["loggedIn"] != null && (bool)Session["loggedIn"];
		}

		private bool IsVerifiedSession()
		{
			return IsLoggedInSession() && Session["verified"] != null && (bool)Session["verified"];
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public string TestConnection()
		{
			return "Success!";
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public LoginResult EmployeeLogin(string email, string password)
		{
			LoginResult result = new LoginResult();

			try
			{
				string sql = @"
					SELECT employee_ID, is_verified
					FROM employee
					WHERE email = @email
					  AND password_hash = SHA2(@password, 256)
					LIMIT 1;
				";

				using (MySqlConnection con = new MySqlConnection(getConString()))
				using (MySqlCommand cmd = new MySqlCommand(sql, con))
				{
					cmd.Parameters.AddWithValue("@email", email);
					cmd.Parameters.AddWithValue("@password", password);

					con.Open();
					using (MySqlDataReader rdr = cmd.ExecuteReader())
					{
						if (!rdr.Read())
						{
							result.success = false;
							result.message = "Incorrect email or password. Try again.";
							return result;
						}

						int employeeId = Convert.ToInt32(rdr["employee_ID"]);
						bool isVerified = Convert.ToInt32(rdr["is_verified"]) == 1;

						if (!isVerified)
						{
							Session.Clear();
							result.success = false;
							result.message = "Access Denied";
							return result;
						}

						Session["loggedIn"] = true;
						Session["verified"] = true;
						Session["employeeId"] = employeeId;
						EnsureAnonId();

						result.success = true;
						result.message = "Login successful.";
						return result;
					}
				}
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Server error: " + ex.Message;
				return result;
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public AccessStatusResult GetAccessStatus()
		{
			AccessStatusResult result = new AccessStatusResult();

			bool loggedIn = IsLoggedInSession();
			bool verified = IsVerifiedSession();

			result.loggedIn = loggedIn;
			result.verified = verified;

			if (!loggedIn)
			{
				result.message = "Not logged in.";
			}
			else if (!verified)
			{
				result.message = "Access Denied";
			}
			else
			{
				result.message = "OK";
			}

			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult AddSuggestion(string suggestionText)
		{
			BasicResult result = new BasicResult();

			try
			{
				if (suggestionText == null) suggestionText = "";
				suggestionText = suggestionText.Trim();

				if (suggestionText.Length == 0)
				{
					result.success = false;
					result.message = "Suggestion cannot be empty.";
					return result;
				}

				if (!IsLoggedInSession())
				{
					result.success = false;
					result.message = "You must be logged in to submit a suggestion.";
					return result;
				}

				if (!IsVerifiedSession())
				{
					result.success = false;
					result.message = "Access Denied";
					return result;
				}

				EnsureAnonId();

				string sql = @"
					INSERT INTO Suggestions (SuggestionText, CreatedDate, UpVotes, DownVotes)
					VALUES (@text, NOW(), 0, 0);
				";

				using (MySqlConnection con = new MySqlConnection(getConString()))
				using (MySqlCommand cmd = new MySqlCommand(sql, con))
				{
					cmd.Parameters.AddWithValue("@text", suggestionText);
					con.Open();
					cmd.ExecuteNonQuery();
				}

				result.success = true;
				result.message = "Suggestion submitted.";
				return result;
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Server error: " + ex.Message;
				return result;
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public List<SuggestionRow> GetSuggestions()
		{
			List<SuggestionRow> rows = new List<SuggestionRow>();

			if (!IsVerifiedSession())
			{
				return rows;
			}

			try
			{
				string sql = @"
					SELECT SuggestionID, SuggestionText, CreatedDate, UpVotes, DownVotes
					FROM Suggestions
					ORDER BY CreatedDate DESC
					LIMIT 50;
				";

				using (MySqlConnection con = new MySqlConnection(getConString()))
				using (MySqlCommand cmd = new MySqlCommand(sql, con))
				{
					con.Open();
					using (MySqlDataReader rdr = cmd.ExecuteReader())
					{
						while (rdr.Read())
						{
							rows.Add(new SuggestionRow
							{
								SuggestionID = Convert.ToInt32(rdr["SuggestionID"]),
								SuggestionText = rdr["SuggestionText"].ToString(),
								CreatedDate = Convert.ToDateTime(rdr["CreatedDate"]).ToString("yyyy-MM-dd HH:mm"),
								UpVotes = Convert.ToInt32(rdr["UpVotes"]),
								DownVotes = Convert.ToInt32(rdr["DownVotes"])
							});
						}
					}
				}
			}
			catch
			{
			}

			return rows;
		}

		private VoteQuestionRow GetNextVoteQuestionForAnon(string anonId)
		{
			string sql = @"
				SELECT
					s.SuggestionID AS QuestionID,
					s.SuggestionText AS QuestionText,
					s.CreatedDate,
					s.UpVotes AS RightVotes,
					s.DownVotes AS LeftVotes
				FROM Suggestions s
				WHERE NOT EXISTS (
					SELECT 1
					FROM SuggestionVotes sv
					WHERE sv.SuggestionID = s.SuggestionID
					  AND sv.AnonID = @anonId
				)
				ORDER BY s.CreatedDate DESC
				LIMIT 1;
			";

			using (MySqlConnection con = new MySqlConnection(getConString()))
			using (MySqlCommand cmd = new MySqlCommand(sql, con))
			{
				cmd.Parameters.AddWithValue("@anonId", anonId);
				con.Open();

				using (MySqlDataReader rdr = cmd.ExecuteReader())
				{
					if (!rdr.Read())
					{
						return null;
					}

					return new VoteQuestionRow
					{
						QuestionID = Convert.ToInt32(rdr["QuestionID"]),
						QuestionText = rdr["QuestionText"].ToString(),
						CreatedDate = Convert.ToDateTime(rdr["CreatedDate"]).ToString("yyyy-MM-dd HH:mm"),
						RightVotes = Convert.ToInt32(rdr["RightVotes"]),
						LeftVotes = Convert.ToInt32(rdr["LeftVotes"])
					};
				}
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public VoteQuestionRow GetNextVoteQuestion()
		{
			if (!IsVerifiedSession())
			{
				return null;
			}

			try
			{
				string anonId = EnsureAnonId();
				return GetNextVoteQuestionForAnon(anonId);
			}
			catch
			{
				return null;
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public VoteQuestionResult VoteQuestion(int questionId, bool voteRight)
		{
			VoteQuestionResult result = new VoteQuestionResult();

			if (!IsLoggedInSession())
			{
				result.success = false;
				result.message = "You must be logged in to vote.";
				result.hasNext = false;
				return result;
			}

			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				result.hasNext = false;
				return result;
			}

			string anonId = EnsureAnonId();

			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();

					string insertVote = @"
						INSERT INTO SuggestionVotes (SuggestionID, AnonID, IsUpvote)
						VALUES (@id, @anonId, @isUpvote);
					";

					using (MySqlCommand cmd = new MySqlCommand(insertVote, con))
					{
						cmd.Parameters.AddWithValue("@id", questionId);
						cmd.Parameters.AddWithValue("@anonId", anonId);
						cmd.Parameters.AddWithValue("@isUpvote", voteRight);
						cmd.ExecuteNonQuery();
					}

					string updateSql = voteRight
						? "UPDATE Suggestions SET UpVotes = UpVotes + 1 WHERE SuggestionID = @id"
						: "UPDATE Suggestions SET DownVotes = DownVotes + 1 WHERE SuggestionID = @id";

					using (MySqlCommand updateCmd = new MySqlCommand(updateSql, con))
					{
						updateCmd.Parameters.AddWithValue("@id", questionId);
						updateCmd.ExecuteNonQuery();
					}
				}

				result.success = true;
				result.message = "Vote recorded.";
			}
			catch (MySqlException)
			{
				result.success = false;
				result.message = "You have already voted on this question.";
			}

			try
			{
				result.nextQuestion = GetNextVoteQuestionForAnon(anonId);
				result.hasNext = result.nextQuestion != null;
			}
			catch
			{
				result.nextQuestion = null;
				result.hasNext = false;
			}

			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult VoteSuggestion(int suggestionId, bool isUpvote)
		{
			BasicResult result = new BasicResult();

			if (!IsLoggedInSession())
			{
				result.success = false;
				result.message = "You must be logged in to vote.";
				return result;
			}

			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				return result;
			}

			string anonId = EnsureAnonId();

			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();

					string insertVote = @"
						INSERT INTO SuggestionVotes (SuggestionID, AnonID, IsUpvote)
						VALUES (@id, @anonId, @isUpvote);
					";

					using (MySqlCommand cmd = new MySqlCommand(insertVote, con))
					{
						cmd.Parameters.AddWithValue("@id", suggestionId);
						cmd.Parameters.AddWithValue("@anonId", anonId);
						cmd.Parameters.AddWithValue("@isUpvote", isUpvote);
						cmd.ExecuteNonQuery();
					}

					string updateSql = isUpvote
						? "UPDATE Suggestions SET UpVotes = UpVotes + 1 WHERE SuggestionID = @id"
						: "UPDATE Suggestions SET DownVotes = DownVotes + 1 WHERE SuggestionID = @id";

					using (MySqlCommand updateCmd = new MySqlCommand(updateSql, con))
					{
						updateCmd.Parameters.AddWithValue("@id", suggestionId);
						updateCmd.ExecuteNonQuery();
					}

					result.success = true;
					result.message = "Vote recorded.";
				}
			}
			catch (MySqlException)
			{
				result.success = false;
				result.message = "You have already voted on this suggestion.";
			}

			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult Logout()
		{
			BasicResult result = new BasicResult();
			Session.Clear();
			result.success = true;
			result.message = "Logged out.";
			return result;
		}
	}
}
