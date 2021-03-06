﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;

namespace fqopy
{
	class CopyFilesUtility
	{
		static Crc32 Crc32
		{
			get
			{
				return ( _crc32 == null ) ? _crc32 = new Crc32() : _crc32;
			}
		}
		static Crc32 _crc32;

		public static IEnumerable<FqopyResultsItem> CopyFiles( string source, string destination, IEnumerable<string> files, bool fast, bool overwrite = true ) 
		{
			var start = DateTime.Now;

			foreach ( string file in files )
			{
				var dest = file.Replace( source, destination );
				var item = new FqopyResultsItem() { Source = file, Destination = dest };

				var root = Path.GetDirectoryName( dest );
				bool dirflag = Directory.Exists( root );
				if ( !dirflag )
				{
					try
					{
						Directory.CreateDirectory( root );
						dirflag = true;
					}
					catch ( UnauthorizedAccessException ex )
					{
						var er = new ErrorRecord( ex, "6", ErrorCategory.SecurityError, dest );
						item.ErrorMessage = er.Exception.Message;
					}
					catch ( PathTooLongException ex )
					{
						var er = new ErrorRecord( ex, "9", ErrorCategory.WriteError, dest );
						item.ErrorMessage = er.Exception.Message;
					}
					catch ( ArgumentNullException ex )
					{
						var er = new ErrorRecord( ex, "1", ErrorCategory.InvalidArgument, dest );
						item.ErrorMessage = er.Exception.Message;
					}
					catch ( ArgumentException ex )
					{
						var er = new ErrorRecord( ex, "1", ErrorCategory.InvalidArgument, dest );
						item.ErrorMessage = er.Exception.Message;
					}
					catch ( DirectoryNotFoundException ex )
					{
						var er = new ErrorRecord( ex, "2", ErrorCategory.ObjectNotFound, dest );
						item.ErrorMessage = er.Exception.Message;
					}
					catch ( NotSupportedException ex )
					{
						var er = new ErrorRecord( ex, "7", ErrorCategory.InvalidOperation, dest );
						item.ErrorMessage = er.Exception.Message;
					}
					catch ( IOException ex )
					{
						var er = new ErrorRecord( ex, "9", ErrorCategory.WriteError, dest );
						item.ErrorMessage = er.Exception.Message;
					}
				}

				if ( dirflag )
				{
					using ( FileStream sourceStream = File.Open( file, FileMode.Open, FileAccess.Read, FileShare.Read ) )
					{
						try
						{
							foreach ( byte b in Crc32.ComputeHash( sourceStream ) )
							{
								item.SourceCRC += b.ToString( "x2" ).ToLower();
							}

							using ( FileStream destinStream = File.Open( dest, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None ) )
							{
								bool copyFlag = false;

								if ( sourceStream.Length > 0 && ( destinStream.Length == 0 || overwrite ) )
								{
									copyFlag = true;
								}

								if ( destinStream.Length > 0 && overwrite )
								{
									destinStream.SetLength( 0 );
									destinStream.Flush();
									copyFlag = true;
								}

								if ( copyFlag )
								{
									sourceStream.Position = 0;
									destinStream.Position = 0;
									sourceStream.CopyTo( destinStream );
								}

								destinStream.Position = 0;
								foreach ( byte b in Crc32.ComputeHash( destinStream ) )
								{
									item.DestinationCRC += b.ToString( "x2" ).ToLower();
								}
								item.Size = destinStream.Length;
							}
						}
						catch ( UnauthorizedAccessException ex )
						{
							var er = new ErrorRecord( ex, "4", ErrorCategory.PermissionDenied, dest );
							item.ErrorMessage = er.Exception.Message;
						}
						catch ( NotSupportedException ex )
						{
							var er = new ErrorRecord( ex, "7", ErrorCategory.InvalidOperation, sourceStream );
							item.ErrorMessage = er.Exception.Message;
						}
						catch ( ObjectDisposedException ex )
						{
							var er = new ErrorRecord( ex, "8", ErrorCategory.ResourceUnavailable, sourceStream );
							item.ErrorMessage = er.Exception.Message;
						}
						catch ( IOException ex )
						{
							var er = new ErrorRecord( ex, "9", ErrorCategory.WriteError, dest );
							item.ErrorMessage = er.Exception.Message;
						}
					}

					if ( !fast )
					{
						File.SetCreationTimeUtc( dest, File.GetCreationTimeUtc( file ) );
						File.SetLastWriteTimeUtc( dest, File.GetLastWriteTimeUtc( file ) );
						File.SetLastAccessTimeUtc( dest, File.GetLastAccessTimeUtc( file ) );
					}
				}
				item.Time = DateTime.Now - start;
				item.Match = item.SourceCRC == item.DestinationCRC;
				yield return item;
			}
		}
	}
}
