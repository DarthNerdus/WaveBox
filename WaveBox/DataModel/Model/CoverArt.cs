﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using System.Data.SqlTypes;
using WaveBox.DataModel.Singletons;
using WaveBox.DataModel.Model;
using System.Security.Cryptography;
using TagLib;
using Newtonsoft.Json;

namespace WaveBox.DataModel.Model
{
	public class CoverArt
	{
		public const string ART_PATH = "art";
		public const string TMP_ART_PATH = "art/tmp";

		/// <summary>
		/// Properties
		/// </summary>

		private int _artId;
		[JsonProperty("artId")]
		public int ArtId
		{
			get
			{
				return _artId;
			}

			set
			{
				_artId = value;
			}
		}

		private long _adlerHash;
		[JsonProperty("adlerHash")]
		public long AdlerHash
		{
			get
			{
				return _adlerHash;
			}

			set
			{
				_adlerHash = value;
			}
		}

		public string artFile()
		{
			string artf = ART_PATH + Path.DirectorySeparatorChar + AdlerHash;
			return artf;
		}

		/// <summary>
		/// Constructors
		/// </summary>

		public CoverArt()
		{
		}

		public CoverArt(int artId)
		{
			SQLiteConnection conn = null;
			SQLiteDataReader reader = null;

			try
			{
				var q = new SQLiteCommand("SELECT * FROM art WHERE art_id = @artid");

				q.Parameters.AddWithValue("@artid", artId);

				Database.dbLock.WaitOne();
				conn = Database.getDbConnection();
				q.Connection = conn;
				q.Prepare();
				reader = q.ExecuteReader();

				if (reader.Read())
				{
					_artId = reader.GetInt32(0);
					_adlerHash = reader.GetInt64(1);
				}

				reader.Close();
				Database.dbLock.ReleaseMutex();
			}

			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}

			finally
			{
				Database.close(conn, reader);
			}
		}

		// used for getting art from a file.
		public CoverArt(FileStream fs)
		{
			// create an array the length of the file
			byte[] data = new byte[fs.Length];

			// read the data in
			fs.Read(data, 0, Convert.ToInt32(fs.Length));

			// compute the hash of the data
			var md5 = new MD5CryptoServiceProvider();
			_adlerHash = BitConverter.ToInt64(md5.ComputeHash(data), 0);

			_checkDatabaseAndPerformCopy(data);
		}

		// used for getting art from a tag.
		public CoverArt(FileInfo af)
		{
			var file = TagLib.File.Create(af.FullName);
			if (file.Tag.Pictures.Length > 0)
			{
				var data = file.Tag.Pictures[0].Data.Data;
				var md5 = new MD5CryptoServiceProvider();
				_adlerHash = BitConverter.ToInt64(md5.ComputeHash(data), 0);

				_checkDatabaseAndPerformCopy(data);
			}
		}

		private void _checkDatabaseAndPerformCopy(byte[] data)
		{
				SQLiteConnection conn = null;
				SQLiteDataReader reader = null;

				try
				{
					var q = new SQLiteCommand("SELECT * FROM art WHERE adler_hash = @adlerhash");
					q.Parameters.AddWithValue("@adlerhash", AdlerHash);

					Database.dbLock.WaitOne();
					conn = Database.getDbConnection();
					q.Connection = conn;
					q.Prepare();
					reader = q.ExecuteReader();

					if (reader.Read())
					{
						// the art is already in the database
						_artId = reader.GetInt32(reader.GetOrdinal("art_id"));
						try
						{
							Database.dbLock.ReleaseMutex();
						}

						catch (Exception e)
						{
							Console.WriteLine(e.ToString());
						}
					}

					// the art is not already in the database
					else
					{
						try
						{
							Database.dbLock.ReleaseMutex();
						}

						catch (Exception e)
						{
							Console.WriteLine(e.ToString());
						}
						try
						{
							System.IO.File.WriteAllBytes(ART_PATH + _adlerHash, data);
						}

						catch (Exception e)
						{
							Console.WriteLine(e.ToString());
						}

						finally
						{
							Database.close(conn, reader);
						}

						try
						{
							var q1 = new SQLiteCommand("INSERT INTO art (adler_hash) VALUES (@adlerhash)");

							q1.Parameters.AddWithValue("@adlerhash", AdlerHash);

							Database.dbLock.WaitOne();
							var conn1 = Database.getDbConnection();
							q1.Connection = conn1;
							q1.Prepare();
							int result = q1.ExecuteNonQuery();

							if (result < 1)
							{
								Console.WriteLine("Something went wrong with the art insert: ");
							}

							try
							{
								q1.CommandText = "SELECT @@IDENTITY";
								_artId = Convert.ToInt32((q1.ExecuteScalar()).ToString());
							}

							catch (Exception e)
							{
								Console.WriteLine("\r\n\r\nGetting identity: " + e.ToString() + "\r\n\r\n");
							}

							finally
							{
								try
								{
									Database.dbLock.ReleaseMutex();
								}

								catch (Exception e)
								{
									Console.WriteLine(e.ToString());
								}
							}
						}

						catch (SQLiteException e)
						{
							Console.WriteLine("\r\n\r\n" + e.Message + "\r\n\r\n");
						}
					}
				}

				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}

				finally
				{

					Database.close(conn, reader);
				}
		}
	}
}
