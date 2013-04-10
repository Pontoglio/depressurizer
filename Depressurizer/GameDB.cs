﻿/*
Copyright 2011, 2012, 2013 Steve Labbe.

This file is part of Depressurizer.

Depressurizer is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Depressurizer is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Depressurizer.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using Rallion;

namespace Depressurizer {

    public enum AppType {
        New,
        Game,
        DLC,
        IdRedirect,
        NonApp,
        NotFound,
        AgeGated,
        SiteError,
        WebError,
        Unknown
    }

    public class GameDBEntry {
        public int Id;
        public string Name;
        public string Genre;
        public AppType Type;

        public void ScrapeStore() {
            Type = GameDB.ScrapeStore( Id, out Genre );
        }

    }

    public class GameDB {
        public Dictionary<int, GameDBEntry> Games = new Dictionary<int, GameDBEntry>();
        static char[] genreSep = new char[] { ',' };

        #region Accessors
        public bool Contains( int id ) {
            return Games.ContainsKey( id );
        }

        public bool IsDlc( int id ) {
            return Games.ContainsKey( id ) && Games[id].Type == AppType.DLC;
        }

        public string GetName( int id ) {
            if( Games.ContainsKey( id ) ) {
                return Games[id].Name;
            } else {
                return null;
            }
        }

        public string GetGenre( int id, bool full ) {
            if( Games.ContainsKey( id ) ) {
                string fullString = Games[id].Genre;
                if( string.IsNullOrEmpty( fullString ) ) {
                    return null;
                } else if( full ) {
                    return fullString;
                } else {
                    return TruncateGenre( fullString );
                }
            } else {
                return null;
            }
        }

        #endregion

        #region Operations
        public void UpdateAppList() {
            XmlDocument doc = FetchAppList();
            IntegrateAppList( doc );
        }

        public static XmlDocument FetchAppList() {
            XmlDocument doc = new XmlDocument();
            Program.Logger.Write( Rallion.LoggerLevel.Info, "Downloading Steam app list." );
            WebRequest req = WebRequest.Create( @"http://api.steampowered.com/ISteamApps/GetAppList/v0002/?format=xml" );
            using( WebResponse resp = req.GetResponse() ) {
                doc.Load( resp.GetResponseStream() );
            }
            Program.Logger.Write( LoggerLevel.Info, "XML App list downloaded." );
            return doc;
        }

        public int IntegrateAppList( XmlDocument doc ) {
            int added = 0;
            foreach( XmlNode node in doc.SelectNodes( "/applist/apps/app" ) ) {
                int appId;
                if( XmlUtil.TryGetIntFromNode( node["appid"], out appId ) ) {
                    string gameName = XmlUtil.GetStringFromNode( node["name"], null );
                    if( Games.ContainsKey( appId ) ) {
                        GameDBEntry g = Games[appId];
                        if( string.IsNullOrEmpty( g.Name ) || g.Name != gameName ) {
                            g.Name = gameName;
                            g.Type = AppType.New;
                        }
                    } else {
                        GameDBEntry g = new GameDBEntry();
                        g.Id = appId;
                        g.Name = gameName;
                        Games.Add( appId, g );
                        added++;
                    }
                }
            }
            Program.Logger.Write( LoggerLevel.Info, "Loaded {0} new items from the app list.", added );
            return added;
        }
        #endregion

        #region Serialization
        public void SaveToXml( string path ) {
            Program.Logger.Write( LoggerLevel.Info, "Saving GameDB to {0}", path );
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.CloseOutput = true;
            XmlWriter writer = XmlWriter.Create( path, settings );
            writer.WriteStartDocument();
            writer.WriteStartElement( "gamelist" );
            foreach( GameDBEntry g in Games.Values ) {
                writer.WriteStartElement( "game" );

                writer.WriteElementString( "id", g.Id.ToString() );
                if( !string.IsNullOrEmpty( g.Name ) ) {
                    writer.WriteElementString( "name", g.Name );
                }
                writer.WriteElementString( "type", g.Type.ToString() );
                if( !string.IsNullOrEmpty( g.Genre ) ) {
                    writer.WriteElementString( "genre", g.Genre );
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Close();
            Program.Logger.Write( LoggerLevel.Info, "GameDB saved." );
        }

        public void LoadFromXml( string path ) {
            Program.Logger.Write( LoggerLevel.Info, "Loading GameDB from {0}", path );
            XmlDocument doc = new XmlDocument();
            doc.Load( path );
            Program.Logger.Write( LoggerLevel.Info, "GameDB XML document parsed." );
            Games.Clear();

            foreach( XmlNode gameNode in doc.SelectNodes( "/gamelist/game" ) ) {
                int id;
                if( !XmlUtil.TryGetIntFromNode( gameNode["id"], out id ) || Games.ContainsKey( id ) ) {
                    continue;
                }
                GameDBEntry g = new GameDBEntry();
                g.Id = id;
                XmlUtil.TryGetStringFromNode( gameNode["name"], out g.Name );
                string typeString;
                if( !XmlUtil.TryGetStringFromNode( gameNode["type"], out typeString ) || !Enum.TryParse<AppType>( typeString, out g.Type ) ) {
                    g.Type = AppType.New;
                }

                g.Genre = XmlUtil.GetStringFromNode( gameNode["genre"], null );

                Games.Add( id, g );
            }
            Program.Logger.Write( LoggerLevel.Info, "GameDB XML processed, load complete." );
        }
        #endregion

        #region Statics

        private static Regex regGamecheck = new Regex( "<a[^>]*>All Games</a>", RegexOptions.IgnoreCase | RegexOptions.Compiled );
        private static Regex regGenre = new Regex( "<div class=\\\"glance_details\\\">\\s*<div>\\s*Genre:\\s*(<a[^>]*>([^<]+)</a>,?\\s*)+\\s*<br>\\s*</div>", RegexOptions.Compiled | RegexOptions.IgnoreCase );
        private static Regex regDLC = new Regex( "<div class=\\\"name\\\">Downloadable Content</div>", RegexOptions.IgnoreCase | RegexOptions.Compiled );

        public static AppType ScrapeStore( int id, out string genre, int redirectCount = 0 ) {
            Program.Logger.Write( LoggerLevel.Verbose, "Initiating store scrape for game id {0}", id );
            genre = null;
            bool redirect = (redirectCount > 0);
            string page = "";

            int redirectTarget = 0;
            bool needsRedirect = false;

            try {
                HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create( string.Format( Properties.Resources.SteamStoreURL, id ) );
                // Cookie bypasses the age gate
                req.CookieContainer = new CookieContainer( 1 );
                req.CookieContainer.Add( new Cookie( "birthtime", "-2208959999", "/", "store.steampowered.com" ) );

                using( WebResponse resp = req.GetResponse() ) {
                    if( resp.ResponseUri.Segments.Length <= 1 ) {
                        // Redirected to the store front page
                        Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Redirected to main store page", id );
                        return AppType.NotFound;
                    } else if( resp.ResponseUri.Segments.Length >= 2 && resp.ResponseUri.Segments[1] == "agecheck/" ) {
                        if( redirectCount <= 3 && resp.ResponseUri.Segments.Length >= 4 && !resp.ResponseUri.Segments[3].StartsWith( id.ToString() ) ) {
                            // We got an age check for a different ID than we requested
                            if( int.TryParse( resp.ResponseUri.Segments[3].TrimEnd( '/' ), out redirectTarget ) ) {
                                Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Hit age check for id {1}", id, redirectTarget );
                                needsRedirect = true;
                            } else {
                            // Age check without numeric id
                                Program.Logger.Write( LoggerLevel.Warning, "Scraping {0}: Stuck at age gate, redirect with no number (URL: {1})", id, resp.ResponseUri );
                                return AppType.AgeGated;
                            }
                        } else {
                            // Age check with no redirect
                            Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Age check with no redirect, or too many redirects (on #{1})", id, redirectCount );
                            return AppType.AgeGated;
                        }
                    } else if( resp.ResponseUri.Segments.Length < 2 || resp.ResponseUri.Segments[1] != "app/" ) {
                        // Redirected outside of the app path
                        Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Redirected to a non-app URL", id );
                        return AppType.NonApp;
                    } else if( resp.ResponseUri.Segments.Length < 3 || !resp.ResponseUri.Segments[2].StartsWith( id.ToString() ) ) {
                        // Redirected to a different app id, but we still want to check the genre
                        Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Redirected to another app id ({1})", id, resp.ResponseUri.Segments.Length >= 3 ? resp.ResponseUri.Segments[2] : "unknown" );
                        redirect = true;
                    }

                    if( !needsRedirect ) {
                        StreamReader sr = new StreamReader( resp.GetResponseStream() );
                        page = sr.ReadToEnd();
                        Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Page read", id );
                    }
                }
            } catch( Exception e ) {
                // Something went wrong with the download.
                Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Page read failed. {1}", id, e.Message );
                return AppType.WebError;
            }

            if( needsRedirect ) {
                Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Redirecting to {1}", id, redirectTarget );
                return ScrapeStore( redirectTarget, out genre, redirectCount + 1 );
            }

            if( page.Contains( "<title>Site Error</title>" ) ) {
                Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Received Site Error", id );
                return AppType.SiteError;
            }

            // Here we should have an app, but we want to make sure.
            if( regGamecheck.IsMatch( page ) ) {
                string newCat;
                if( GetGenreFromPage( page, out newCat ) ) {
                    genre = newCat;
                }
                // We have a genre, but it could be DLC
                if( GetDLCFromPage( page ) ) {
                    Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Parsed. DLC. Genre: {1}", id, genre );
                    return AppType.DLC;
                } else {
                    Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Parsed. Genre: {1}. Redirect: {2}", id, genre, redirect );
                    return redirect ? AppType.IdRedirect : AppType.Game;
                }
            } else {
                // we don't know what it is.
                Program.Logger.Write( LoggerLevel.Verbose, "Scraping {0}: Could not parse info from page", id );
                return AppType.Unknown;
            }
        }

        private static bool GetGenreFromPage( string page, out string cat ) {
            cat = null;
            Match m = regGenre.Match( page );
            if( m.Success ) {
                int genres = m.Groups[2].Captures.Count;
                string[] array = new string[genres];
                for( int i = 0; i < genres; i++ ) {
                    array[i] = m.Groups[2].Captures[i].Value;
                }
                cat = string.Join( ", ", array );
                return true;
            }
            return false;
        }

        private static bool GetDLCFromPage( string page ) {
            return regDLC.IsMatch( page );
        }

        public static string TruncateGenre( string fullString ) {
            if( fullString == null ) return null;
            int index = fullString.IndexOf( ',' );
            if( index < 0 ) {
                return fullString;
            } else {
                return fullString.Substring( 0, index );
            }
        }

        #endregion
    }
}
