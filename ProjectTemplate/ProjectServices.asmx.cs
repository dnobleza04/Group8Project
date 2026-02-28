using System;
using System.Collections.Generic;
using System.Linq;
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

		private HashSet<int> GetSkippedQuestionIdsSession()
		{
			if (Session["skippedQuestionIds"] == null)
			{
				Session["skippedQuestionIds"] = new HashSet<int>();
			}

			return (HashSet<int>)Session["skippedQuestionIds"];
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
			string loginStep = "start";

			try
			{
				loginStep = "open_connection";
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();

					Func<object, bool> parseVerified = (verifiedObj) =>
					{
						if (verifiedObj == null || verifiedObj == DBNull.Value)
						{
							return true;
						}

						if (verifiedObj is bool)
						{
							return (bool)verifiedObj;
						}

						string verifiedText = Convert.ToString(verifiedObj);
						return verifiedText == "1"
							|| verifiedText.Equals("true", StringComparison.OrdinalIgnoreCase)
							|| verifiedText.Equals("y", StringComparison.OrdinalIgnoreCase)
							|| verifiedText.Equals("yes", StringComparison.OrdinalIgnoreCase);
					};

					Func<string, bool, bool> tryLoginQuery = (verifyColumn, hashPassword) =>
					{
						string passwordExpr = hashPassword
							? "password_hash = SHA2(@password, 256)"
							: "password = @password";

						string sql = (verifyColumn != null)
							? "SELECT employee_ID, " + verifyColumn + " FROM employee WHERE email = @email AND " + passwordExpr + " LIMIT 1;"
							: "SELECT employee_ID, 1 FROM employee WHERE email = @email AND " + passwordExpr + " LIMIT 1;";

						try
						{
							loginStep = "execute_login_query";
							using (MySqlCommand cmd = new MySqlCommand(sql, con))
							{
								cmd.Parameters.AddWithValue("@email", email);
								cmd.Parameters.AddWithValue("@password", password);

								using (MySqlDataReader rdr = cmd.ExecuteReader())
								{
									if (!rdr.Read())
									{
										return false;
									}

									int employeeId = Convert.ToInt32(rdr.GetValue(0));
									bool isVerified = parseVerified(rdr.GetValue(1));

									if (!isVerified)
									{
										Session.Clear();
										result.success = false;
										result.message = "Access Denied";
										return true;
									}

									Session["loggedIn"] = true;
									Session["verified"] = true;
									Session["employeeId"] = employeeId;
									loginStep = "ensure_anon_id";
									EnsureAnonId();

									result.success = true;
									result.message = "Login successful.";
									return true;
								}
							}
						}
						catch (MySqlException ex)
						{
							if (ex.Number == 1054 || ex.Message.IndexOf("Unknown column", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								return false;
							}

							throw;
						}
					};

					if (tryLoginQuery("is_verified", true)) return result;
					if (tryLoginQuery("verified", true)) return result;
					if (tryLoginQuery("isVerified", true)) return result;
					if (tryLoginQuery("IsVerified", true)) return result;
					if (tryLoginQuery(null, true)) return result;

					if (tryLoginQuery("is_verified", false)) return result;
					if (tryLoginQuery("verified", false)) return result;
					if (tryLoginQuery("isVerified", false)) return result;
					if (tryLoginQuery("IsVerified", false)) return result;
					if (tryLoginQuery(null, false)) return result;

					result.success = false;
					result.message = "Incorrect email or password. Try again.";
					return result;
				}
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Server error at " + loginStep + ": " + ex.GetType().Name + " - " + ex.Message;
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
		public BasicResult SubmitAnonymousFeedback(string feedbackText)
		{
			return AddSuggestion(feedbackText);
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

		private VoteQuestionRow GetNextVoteQuestionForAnon(string anonId, HashSet<int> skippedIds = null)
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
			";

			if (skippedIds != null && skippedIds.Count > 0)
			{
				string[] skipParams = skippedIds.Select((id, i) => "@skip" + i).ToArray();
				sql += " AND s.SuggestionID NOT IN (" + string.Join(",", skipParams) + ")";
			}

			sql += " ORDER BY s.CreatedDate DESC LIMIT 1;";

			using (MySqlConnection con = new MySqlConnection(getConString()))
			using (MySqlCommand cmd = new MySqlCommand(sql, con))
			{
				cmd.Parameters.AddWithValue("@anonId", anonId);

				if (skippedIds != null && skippedIds.Count > 0)
				{
					int i = 0;
					foreach (int id in skippedIds)
					{
						cmd.Parameters.AddWithValue("@skip" + i, id);
						i++;
					}
				}

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
				HashSet<int> skippedIds = GetSkippedQuestionIdsSession();
				return GetNextVoteQuestionForAnon(anonId, skippedIds);
			}
			catch
			{
				return null;
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public VoteQuestionResult SkipQuestion(int questionId)
		{
			VoteQuestionResult result = new VoteQuestionResult();

			if (!IsLoggedInSession())
			{
				result.success = false;
				result.message = "You must be logged in to skip.";
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

			if (questionId <= 0)
			{
				result.success = false;
				result.message = "Invalid question.";
				result.hasNext = false;
				return result;
			}

			string anonId = EnsureAnonId();
			HashSet<int> skippedIds = GetSkippedQuestionIdsSession();
			skippedIds.Add(questionId);

			result.success = true;
			result.message = "Question skipped.";

			try
			{
				result.nextQuestion = GetNextVoteQuestionForAnon(anonId, skippedIds);
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
			HashSet<int> skippedIds = GetSkippedQuestionIdsSession();

			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();
					using (MySqlTransaction tx = con.BeginTransaction())
					{
						string existsSql = "SELECT COUNT(*) FROM Suggestions WHERE SuggestionID = @id";

						using (MySqlCommand existsCmd = new MySqlCommand(existsSql, con, tx))
						{
							existsCmd.Parameters.AddWithValue("@id", questionId);
							int exists = Convert.ToInt32(existsCmd.ExecuteScalar());
							if (exists == 0)
							{
								tx.Rollback();
								result.success = false;
								result.message = "Question not found.";
								result.hasNext = false;
								return result;
							}
						}

						string alreadySql = @"
							SELECT COUNT(*)
							FROM SuggestionVotes
							WHERE SuggestionID = @id AND AnonID = @anonId;
						";

						using (MySqlCommand alreadyCmd = new MySqlCommand(alreadySql, con, tx))
						{
							alreadyCmd.Parameters.AddWithValue("@id", questionId);
							alreadyCmd.Parameters.AddWithValue("@anonId", anonId);
							int alreadyCount = Convert.ToInt32(alreadyCmd.ExecuteScalar());
							if (alreadyCount > 0)
							{
								tx.Rollback();
								result.success = false;
								result.message = "You have already voted on this question.";
								result.hasNext = false;
								return result;
							}
						}

					string insertVote = @"
						INSERT INTO SuggestionVotes (SuggestionID, AnonID, IsUpvote)
						VALUES (@id, @anonId, @isUpvote);
					";

					using (MySqlCommand cmd = new MySqlCommand(insertVote, con, tx))
					{
						cmd.Parameters.AddWithValue("@id", questionId);
						cmd.Parameters.AddWithValue("@anonId", anonId);
						cmd.Parameters.AddWithValue("@isUpvote", voteRight);
						cmd.ExecuteNonQuery();
					}

					string updateSql = voteRight
						? "UPDATE Suggestions SET UpVotes = UpVotes + 1 WHERE SuggestionID = @id"
						: "UPDATE Suggestions SET DownVotes = DownVotes + 1 WHERE SuggestionID = @id";

					using (MySqlCommand updateCmd = new MySqlCommand(updateSql, con, tx))
					{
						updateCmd.Parameters.AddWithValue("@id", questionId);
						updateCmd.ExecuteNonQuery();
					}

						tx.Commit();
					}
				}

				result.success = true;
				result.message = "Vote recorded.";
				skippedIds.Remove(questionId);
			}
			catch (MySqlException)
			{
				result.success = false;
				result.message = "Vote could not be recorded.";
			}

			try
			{
				result.nextQuestion = GetNextVoteQuestionForAnon(anonId, skippedIds);
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
