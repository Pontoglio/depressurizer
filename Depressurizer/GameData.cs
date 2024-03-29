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
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using Rallion;

namespace Depressurizer {
    /// <summary>
    /// Represents a single game
    /// </summary>
    public class Game {
        public string Name;
        public int Id;
        public Category Category;
        public bool Favorite;

        private string _launchStr = null;
        public string LaunchString {
            get {
                if( Id > 0 ) return Id.ToString();
                if( !string.IsNullOrEmpty( _launchStr ) ) return _launchStr;
                return null;
            }
            set {
                _launchStr = value;
            }
        }


        public Game( int id, string name ) {
            Id = id;
            Name = name;
            Category = null;
            Favorite = false;
        }
    }

    /// <summary>
    /// Represents a single game category
    /// </summary>
    public class Category : IComparable {
        public string Name;

        public Category( string name ) {
            Name = name;
        }

        public override string ToString() {
            return Name;
        }

        public int CompareTo( object o ) {
            //TODO: make 'favorite' come first, and maybe fix that string sorting thing
            return Name.CompareTo( ( o as Category ).Name );
        }
    }

    /// <summary>
    /// Represents a complete collection of games and categories.
    /// </summary>
    public class GameData {
        #region Fields
        public Dictionary<int, Game> Games;
        public List<Category> Categories;

        private static Regex rxUnicode = new Regex( @"\\u(?<Value>[a-zA-Z0-9]{4})", RegexOptions.Compiled );
        #endregion

        public GameData() {
            Games = new Dictionary<int, Game>();
            Categories = new List<Category>();
        }

        #region Modifiers
        public void Clear() {
            Games.Clear();
            Categories.Clear();
        }

        /// <summary>
        /// Sets the name of the given game ID, and adds the game to the list if it doesn't already exist.
        /// </summary>
        /// <param name="id">ID of the game to set</param>
        /// <param name="name">Name to assign to the game</param>
        /// <returns>True if game was not already in the list, false otherwise</returns>
        private bool SetGameName( int id, string name, bool overWrite ) {
            if( !Games.ContainsKey( id ) ) {
                Games.Add( id, new Game( id, name ) );
                return true;
            }
            if( overWrite ) {
                Games[id].Name = name;
            }
            return false;
        }

        /// <summary>
        /// Removes a game from the game list.
        /// </summary>
        /// <param name="appId">Id of game to remove.</param>
        /// <returns>True if game was removed, false otherwise</returns>
        private bool RemoveGame( int appId ) {
            bool removed = false;
            if( appId < 0 ) {
                if( Games.ContainsKey( appId ) ) {
                    Game removedGame = Games[appId];
                    removed = Games.Remove( appId );
                    if( removed )
                        Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_RemovedGameFromGameList, appId, removedGame.Name );
                    else
                        Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorRemovingGame, appId, removedGame.Name );
                    return removed;
                }
            } else
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorRemovingSteamGame, appId );
            return removed;
        }

        /// <summary>
        /// Adds a new category to the list.
        /// </summary>
        /// <param name="name">Name of the category to add</param>
        /// <returns>The added category. Returns null if the category already exists.</returns>
        public Category AddCategory( string name ) {
            if( CategoryExists( name ) ) {
                return null;
            } else {
                Category newCat = new Category( name );
                Categories.Add( newCat );
                return newCat;
            }
        }

        /// <summary>
        /// Removes the given category.
        /// </summary>
        /// <param name="c">Category to remove.</param>
        /// <returns>True if removal was successful, false if it was not in the list anyway</returns>
        public bool RemoveCategory( Category c ) {
            if( Categories.Remove( c ) ) {
                foreach( Game g in Games.Values ) {
                    if( g.Category == c ) g.Category = null;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Renames the given category.
        /// </summary>
        /// <param name="c">Category to rename.</param>
        /// <param name="newName">Name to assign to the new category.</param>
        /// <returns>True if rename was successful, false otherwise (if name was in use already)</returns>
        public bool RenameCategory( Category c, string newName ) {
            if( !CategoryExists( newName ) ) {
                c.Name = newName;
                Categories.Sort();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the categories for the given list of game IDs to the same thing
        /// </summary>
        /// <param name="gameIDs">Array of game IDs.</param>
        /// <param name="newCat">Category to assign</param>
        public void SetGameCategories( int[] gameIDs, Category newCat ) {
            for( int i = 0; i < gameIDs.Length; i++ ) {
                Games[gameIDs[i]].Category = newCat;
            }
        }

        /// <summary>
        /// Sets the fav state for the given list of game IDs to the same thing
        /// </summary>
        /// <param name="gameIDs">Array of game IDs.</param>
        /// <param name="newCat">Fav state to assign</param>
        public void SetGameFavorites( int[] gameIDs, bool fav ) {
            for( int i = 0; i < gameIDs.Length; i++ ) {
                Games[gameIDs[i]].Favorite = fav;
            }
        }

        /// <summary>
        /// Remove all empty categories from the category list.
        /// </summary>
        /// <returns>Number of categories removed</returns>
        public int RemoveEmptyCategories() {
            Dictionary<Category, int> counts = new Dictionary<Category, int>();
            foreach( Category c in Categories ) {
                counts.Add( c, 0 );
            }
            foreach( Game g in Games.Values ) {
                if( g.Category != null && counts.ContainsKey( g.Category ) ) {
                    counts[g.Category] = counts[g.Category] + 1;
                }
            }
            int removed = 0;
            foreach( KeyValuePair<Category, int> pair in counts ) {
                if( pair.Value == 0 ) {
                    if( Categories.Remove( pair.Key ) ) {
                        removed++;
                    }
                }
            }
            return removed;
        }
        #endregion

        #region Accessors
        /// <summary>
        /// Checks to see if a category with the given name exists
        /// </summary>
        /// <param name="name">Name of the category to look for</param>
        /// <returns>True if the name is found, false otherwise</returns>
        public bool CategoryExists( string name ) {
            foreach( Category c in Categories ) {
                if( c.Name == name ) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the category with the given name. If the category does not exist, creates it.
        /// </summary>
        /// <param name="name">Name to get the category for</param>
        /// <returns>A category with the given name.</returns>
        public Category GetCategory( string name ) {
            if( string.IsNullOrEmpty( name ) ) return null;
            foreach( Category c in Categories ) {
                if( c.Name == name ) return c;
            }
            Category newCat = new Category( name );
            Categories.Add( newCat );
            return newCat;
        }
        #endregion

        #region Profile Data Fetching
        /// <summary>
        /// Grabs the XML game list for the given account and reads it into an XmlDocument.
        /// </summary>
        /// <param name="customUrl">The custom name for the account</param>
        /// <returns>Fetched XML page as an XmlDocument</returns>
        public static XmlDocument FetchXmlGameList( string customUrl ) {
            return FetchXmlFromUrl( string.Format( Properties.Resources.UrlCustomGameListXml, customUrl ) );
        }

        /// <summary>
        /// Grabs the XML game list for the given account and reads it into an XmlDocument.
        /// </summary>
        /// <param name="accountId">The 64-bit account ID</param>
        /// <returns>Fetched XML page as an XmlDocument</returns>
        public static XmlDocument FetchXmlGameList( Int64 steamId ) {
            return FetchXmlFromUrl( string.Format( Properties.Resources.UrlGameListXml, steamId ) );
        }

        /// <summary>
        /// Fetches an XML game list and loads it into an XML document.
        /// </summary>
        /// <param name="url">The URL to fetch</param>
        /// <returns>Fetched XML page as an XmlDocument</returns>
        public static XmlDocument FetchXmlFromUrl( string url ) {
            XmlDocument doc = new XmlDocument();
            try {
                Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_AttemptingDownloadXMLGameList, url );
                WebRequest req = HttpWebRequest.Create( url );
                WebResponse response = req.GetResponse();
                if( response.ResponseUri.Segments.Length < 4 ) {
                    throw new ProfileAccessException( GlobalStrings.GameData_SpecifiedProfileNotPublic );
                }
                doc.Load( response.GetResponseStream() );
                response.Close();
                Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_SuccessDownloadXMLGameList, url );
                return doc;
            } catch( ProfileAccessException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ProfileNotPublic );
                throw e;
            } catch( Exception e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ExceptionDownloadXMLGameList, e.Message );
                throw new ApplicationException( e.Message, e );
            }
        }

        /// <summary>
        /// Grabs the HTML game list for the given account and returns its full text.
        /// </summary>
        /// <param name="customUrl">The custom name for the account</param>
        /// <returns>Full text of the HTTP response</returns>
        public static string FetchHtmlGameList( string customUrl ) {
            return FetchHtmlFromUrl( string.Format( Properties.Resources.UrlCustomGameListHtml, customUrl ) );
        }

        /// <summary>
        /// Grabs the HTML game list for the given account and returns its full text.
        /// </summary>
        /// <param name="accountId">The 64-bit account ID</param>
        /// <returns>Full text of the HTTP response</returns>
        public static string FetchHtmlGameList( Int64 accountId ) {
            return FetchHtmlFromUrl( string.Format( Properties.Resources.UrlGameListHtml, accountId ) );
        }

        /// <summary>
        /// Fetches an HTML game list and returns the full page text.
        /// Mostly just grabs the given HTTP response, except that it throws an errors if the profile is not public, and writes approrpriate log entries.
        /// </summary>
        /// <param name="url">The URL to fetch</param>
        /// <returns>The full text of the HTML page</returns>
        public static string FetchHtmlFromUrl( string url ) {
            try {
                string result = "";

                Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_AttemptingDownloadHTMLGameList, url );
                WebRequest req = HttpWebRequest.Create( url );
                using( WebResponse response = req.GetResponse() ) {
                    if( response.ResponseUri.Segments.Length < 4 ) {
                        throw new ProfileAccessException( GlobalStrings.GameData_SpecifiedProfileNotPublic );
                    }
                    StreamReader sr = new StreamReader( response.GetResponseStream() );
                    result = sr.ReadToEnd();
                }
                Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_SuccessDownloadHTMLGameList, url );
                return result;
            } catch( ProfileAccessException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ProfileNotPublic );
                throw e;
            } catch( Exception e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ExceptionDownloadHTMLGameList, e.Message );
                throw new ApplicationException( e.Message, e );
            }
        }
        #endregion

        #region Profile Data Integrating
        /// <summary>
        /// Integrates list of games from an XmlDocument into the loaded game list.
        /// </summary>
        /// <param name="doc">The XmlDocument containing the new game list</param>
        /// <param name="overWrite">If true, overwrite the names of games already in the list.</param>
        /// <param name="ignore">A set of item IDs to ignore.</param>
        /// <param name="ignoreDlc">Ignore any items classified as DLC in the database.</param>
        /// <param name="newItems">The number of new items actually added</param>
        /// <returns>Returns the number of games successfully processed and not ignored.</returns>
        public int IntegrateXmlGameList( XmlDocument doc, bool overWrite, SortedSet<int> ignore, bool ignoreDlc, out int newItems ) {
            newItems = 0;
            if( doc == null ) return 0;
            int loadedGames = 0;
            XmlNodeList gameNodes = doc.SelectNodes( "/gamesList/games/game" );
            foreach( XmlNode gameNode in gameNodes ) {
                int appId;
                XmlNode appIdNode = gameNode["appID"];
                if( appIdNode != null && int.TryParse( appIdNode.InnerText, out appId ) ) {
                    XmlNode nameNode = gameNode["name"];
                    if( nameNode != null ) {
                        bool isNew;
                        bool added = IntegrateGame( appId, nameNode.InnerText, overWrite, ignore, ignoreDlc, out isNew );
                        if( added ) {
                            loadedGames++;
                            if( isNew ) {
                                newItems++;
                            }
                        }
                    }
                }
            }
            Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_IntegratedXMLDataIntoGameList, loadedGames, newItems );
            return loadedGames;
        }

        /// <summary>
        /// Integrates list of games from an HTML page into the loaded game list.
        /// </summary>
        /// <param name="page">The full text of the page to load</param>
        /// <param name="overWrite">If true, overwrite the names of games already in the list.</param>
        /// <param name="ignore">A set of item IDs to ignore. Can be null.</param>
        /// <param name="ignoreDlc">Ignore any items classified as DLC in the database.</param>
        /// <param name="newItems">The number of new items actually added</param>
        /// <returns>Returns the number of games successfully processed and not ignored.</returns>
        public int IntegrateHtmlGameList( string page, bool overWrite, SortedSet<int> ignore, bool ignoreDlc, out int newItems ) {
            newItems = 0;
            int totalItems = 0;

            Regex srch = new Regex( "\"appid\":([0-9]+),\"name\":\"([^\"]+)\"" );
            MatchCollection matches = srch.Matches( page );
            foreach( Match m in matches ) {
                if( m.Groups.Count < 3 ) continue;
                string appIdString = m.Groups[1].Value;
                string appName = m.Groups[2].Value;

                int appId;
                if( appName != null && appIdString != null && int.TryParse( appIdString, out appId ) ) {
                    appName = ProcessUnicode( appName );
                    bool isNew;
                    bool added = IntegrateGame( appId, appName, overWrite, ignore, ignoreDlc, out isNew );
                    if( added ) {
                        totalItems++;
                        if( isNew ) {
                            newItems++;
                        }
                    }
                }
            }
            Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_IntegratedHTMLDataIntoGameList, totalItems, newItems );
            return totalItems;
        }

        /// <summary>
        /// Searches a string for HTML unicode entities ('\u####') and replaces them with actual unicode characters.
        /// </summary>
        /// <param name="val">The string to process</param>
        /// <returns>The processed string</returns>
        public string ProcessUnicode( string val ) {
            return rxUnicode.Replace(
                val,
                m => ( (char)int.Parse( m.Groups["Value"].Value, NumberStyles.HexNumber ) ).ToString()
            );
        }

        /// <summary>
        /// Adds a new game to the database, or updates an existing game with new information.
        /// </summary>
        /// <param name="appId">App ID to add or update</param>
        /// <param name="appName">Name of app to add, or update to</param>
        /// <param name="overWrite">If true, will overwrite any existing games. If false, will fail if the game already exists.</param>
        /// <param name="ignore">Set of games to ignore. Can be null. If the game is in this list, no action will be taken.</param>
        /// <param name="ignoreDlc">If true, ignore the game if it is marked as DLC in the loaded database.</param>
        /// <param name="isNew">If true, a new game was added. If false, an existing game was updated, or the operation failed.</param>
        /// <returns>True if the game was integrated, false otherwise.</returns>
        private bool IntegrateGame( int appId, string appName, bool overWrite, SortedSet<int> ignore, bool ignoreDlc, out bool isNew ) {
            isNew = false;
            if( ( ignore != null && ignore.Contains( appId ) ) || ( ignoreDlc && Program.GameDB.IsDlc( appId ) ) ) {
                Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_SkippedIntegratingGame, appId, appName );
                return false;
            }
            isNew = SetGameName( appId, appName, overWrite );
            Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_IntegratedGameIntoGameList, appId, appName, isNew );
            return true;
        }
        #endregion

        #region Steam config file handling
        /// <summary>
        /// Loads category info from the given steam config file.
        /// </summary>
        /// <param name="filePath">The path of the file to open</param>
        /// <returns>The number of game entries found</returns>
        public int ImportSteamFile( string filePath, SortedSet<int> ignore, bool ignoreDlc ) {
            Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_OpeningSteamConfigFile, filePath );
            TextVdfFileNode dataRoot;

            try {
                using( StreamReader reader = new StreamReader( filePath, false ) ) {
                    dataRoot = TextVdfFileNode.Load( reader, true );
                }
            } catch( ParseException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorParsingConfigFileParam, e.Message );
                throw new ApplicationException( GlobalStrings.GameData_ErrorParsingSteamConfigFile + e.Message, e );
            } catch( IOException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorOpeningConfigFileParam, e.Message );
                throw new ApplicationException( GlobalStrings.GameData_ErrorOpeningSteamConfigFile + e.Message, e );
            }

            VdfFileNode appsNode = dataRoot.GetNodeAt( new string[] { "Software", "Valve", "Steam", "apps" }, true );
            int count = GetDataFromVdf( appsNode, ignore, ignoreDlc );
            Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_SteamConfigFileLoaded, count );
            return count;
        }

        /// <summary>
        /// Loads category info from the steam config file for the given Steam user.
        /// </summary>
        /// <param name="SteamId">Identifier of Steam user</param>
        /// <param name="ignore">Set of games to ignore</param>
        /// <param name="ignoreDlc">If true, ignore games marked as DLC in the database</param>
        /// <param name="ignoreExternal">If false, also load non-steam games</param>
        /// <returns>The number of game entries found</returns>
        public int ImportSteamFile( long SteamId, SortedSet<int> ignore, bool ignoreDlc, bool ignoreExternal ) {
            string filePath = string.Format( Properties.Resources.ConfigFilePath, Settings.Instance().SteamPath, Profile.ID64toDirName( SteamId ) );
            int result = ImportSteamFile( filePath, ignore, ignoreDlc );
            if( !ignoreExternal ) {
                int newItems, removedItems;
                result += ImportNonSteamGames( SteamId, true, out newItems, out removedItems );
            }
            return result;
        }

        /// <summary>
        /// Writes category information out Steam user config file for Steam games and external games.
        /// </summary>
        /// <param name="SteamId">Identifier of Steam user</param>
        /// <param name="discardMissing">Delete category information for games not present in game list</param>
        public void SaveSteamFile( long SteamId, bool discardMissing, bool includeShortcuts = true ) {
            string filePath = string.Format( Properties.Resources.ConfigFilePath, Settings.Instance().SteamPath, Profile.ID64toDirName( SteamId ) );
            SaveSteamFile( filePath, discardMissing );
            if( includeShortcuts ) {
                SaveShortcutGames( SteamId, discardMissing );
            }
        }

        /// <summary>
        /// Writes category information out to a steam config file. Also saves any other settings that had been loaded, to avoid setting loss.
        /// </summary>
        /// <param name="path">Full path of the steam config file to save</param>
        public void SaveSteamFile( string filePath, bool discardMissing ) {
            Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_SavingSteamConfigFile, filePath );

            TextVdfFileNode fileData = new TextVdfFileNode();
            try {
                using( StreamReader reader = new StreamReader( filePath, false ) ) {
                    fileData = TextVdfFileNode.Load( reader, true );
                }
            } catch( Exception e ) {
                Program.Logger.Write( LoggerLevel.Warning, GlobalStrings.GameData_LoadingErrorSteamConfig, e.Message );
            }

            VdfFileNode appListNode = fileData.GetNodeAt( new string[] { "Software", "Valve", "Steam", "apps" }, true );

            if( discardMissing ) {
                Dictionary<string, VdfFileNode> gameNodeArray = appListNode.NodeArray;
                if( gameNodeArray != null ) {
                    foreach( KeyValuePair<string, VdfFileNode> pair in gameNodeArray ) {
                        int gameId;
                        if( !( int.TryParse( pair.Key, out gameId ) && Games.ContainsKey( gameId ) ) ) {
                            Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_RemovingGameCategoryFromSteamConfig, gameId );
                            pair.Value.RemoveSubnode( "tags" );
                        }
                    }
                }
            }

            foreach( Game game in Games.Values ) {
                if( game.Id > 0 ) // External games have negative identifier
                {
                    Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_AddingGameToConfigFile, game.Id );
                    VdfFileNode gameNode = (VdfFileNode)appListNode[game.Id.ToString()];
                    gameNode.RemoveSubnode( "tags" );
                    if( game.Category != null || game.Favorite ) {
                        VdfFileNode tagsNode = (VdfFileNode)gameNode["tags"];
                        int key = 0;
                        if( game.Category != null ) {
                            tagsNode[key.ToString()] = new TextVdfFileNode( game.Category.Name );
                            key++;
                        }
                        if( game.Favorite ) {
                            tagsNode[key.ToString()] = new TextVdfFileNode( "favorite" );
                        }
                    }
                }
            }

            Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_CleaningUpSteamConfigTree );
            appListNode.CleanTree();

            Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_WritingToDisk );
            TextVdfFileNode fullFile = new TextVdfFileNode();
            fullFile["UserLocalConfigStore"] = fileData;
            try {
                FileInfo f = new FileInfo( filePath );
                f.Directory.Create();
                FileStream fStream = f.Open( FileMode.Create, FileAccess.Write, FileShare.None );
                using( StreamWriter writer = new StreamWriter( fStream ) ) {
                    fullFile.Save( writer );
                }
                fStream.Close();
            } catch( ArgumentException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorSavingSteamConfigFile, e.ToString() );
                throw new ApplicationException( GlobalStrings.GameData_FailedToSaveSteamConfigBadPath, e );
            } catch( IOException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorSavingSteamConfigFile, e.ToString() );
                throw new ApplicationException( GlobalStrings.GameData_FailedToSaveSteamConfigFile + e.Message, e );
            } catch( UnauthorizedAccessException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorSavingSteamConfigFile, e.ToString() );
                throw new ApplicationException( GlobalStrings.GameData_AccessDeniedSteamConfigFile + e.Message, e );
            }
        }
        #endregion

        #region Non-Steam game file handling

        /* this had too much in common with the other loading function to maintain both
        /// <summary>
        /// Loads all non-steam games into the game list and sets their categories to match the loaded config file
        /// </summary>
        /// <param name="SteamId">64-bit ID of the account to load</param>
        /// <param name="ignore">Set of game IDs to ignore</param>
        /// <returns>Number of games loaded</returns>
        private int ImportNonSteamGames( long SteamId, SortedSet<int> ignore )
        {
            int result = 0;
            string filePath = string.Format(Properties.Resources.ShortCutsFilePath, Settings.Instance().SteamPath, Profile.ID64toDirName(SteamId));
            Program.Logger.Write(LoggerLevel.Info, GlobalStrings.GameData_OpeningSteamConfigFile, filePath);

            Dictionary<string, int> shortcutgames;
            if (LoadShortcutGames(SteamId, out shortcutgames))
            {
                FileStream fStream = null;
                BinaryReader binReader = null;
                BinaryVdfFileNode dataRoot = null;
                try
                {
                    fStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    binReader = new BinaryReader(fStream);
                    dataRoot = BinaryVdfFileNode.Load(binReader);
                }
                finally
                {
                    if (binReader != null)
                    {
                        binReader.Close();
                    }
                    if (fStream != null)
                    {
                        fStream.Close();
                    }
                }

                if (dataRoot != null)
                {
                    VdfFileNode shortcutsNode = dataRoot.GetNodeAt(new string[] { "shortcuts" }, false);
                    foreach (KeyValuePair<string, VdfFileNode> shortcutPair in shortcutsNode.NodeArray)
                    {
                        string indexGame = shortcutPair.Key;

                        VdfFileNode attrGame = shortcutPair.Value;
                        VdfFileNode appGame = attrGame.GetNodeAt(new string[] { "appname" }, false);

                        string gameName = appGame.NodeString;

                        // Check if external game has identifier in screenshots.vdf file (this happens only if game has been launched before from Steam client)
                        if (shortcutgames.ContainsKey(gameName))
                        {
                            int gameIdInDB = shortcutgames[gameName];
                            if (!ignore.Contains(gameIdInDB))
                            {
                                Game gameDB;
                                if (Games.ContainsKey(gameIdInDB))
                                {
                                    gameDB = Games[gameIdInDB];
                                }
                                else
                                {
                                    gameDB = new Game(gameIdInDB, gameName);
                                }

                                string cat0 = null, cat1 = null;
                                VdfFileNode tagsGame = attrGame.GetNodeAt(new string[] { "tags" }, false);
                                if ((tagsGame != null) && (tagsGame.NodeType == ValueType.Array) &&
                                    (tagsGame.NodeArray.Count > 0) && (tagsGame.NodeArray.ContainsKey("0")))
                                {
                                    VdfFileNode vdfCat = tagsGame.NodeArray["0"];
                                    if (vdfCat.NodeType == ValueType.Value)
                                    {
                                        cat0 = vdfCat.NodeData.ToString();
                                    }
                                    if (tagsGame.NodeArray.ContainsKey("1"))
                                    {
                                        vdfCat = tagsGame.NodeArray["1"];
                                        if (vdfCat.NodeType == ValueType.Value)
                                        {
                                            cat1 = vdfCat.NodeData.ToString();
                                        }
                                    }
                                }
                                gameDB.Favorite = ((cat0 == "favorite") || (cat1 == "favorite"));
                                if (cat0 != "favorite")
                                {
                                    gameDB.Category = GetCategory(cat0);
                                }
                                else
                                {
                                    gameDB.Category = GetCategory(cat1);
                                }
                                result++;
                            }
                        }
                    }

                }
            }
            return result;
        }
        */

        /// <summary>
        /// Loads in games from an node containing a list of games.
        /// Any games in the node not found in the game list will be added to the gamelist.
        /// If a game in the node has a tags subnode, the "favorite" field will be overwritten.
        /// If a game in the node has a category set, it will overwrite any categories in the gamelist.
        /// If a game in the node does NOT have a category set, the category in the gamelist will NOT be cleared.
        /// </summary>
        /// <param name="appsNode">Node containing the game nodes</param>
        /// <param name="ignore">Set of games to ignore</param>
        /// <returns>Number of games loaded</returns>
        private int GetDataFromVdf( VdfFileNode appsNode, SortedSet<int> ignore, bool ignoreDlc ) {
            int loadedGames = 0;

            Dictionary<string, VdfFileNode> gameNodeArray = appsNode.NodeArray;
            if( gameNodeArray != null ) {
                foreach( KeyValuePair<string, VdfFileNode> gameNodePair in gameNodeArray ) {
                    int gameId;
                    if( int.TryParse( gameNodePair.Key, out gameId ) ) {
                        if( ( ignore != null && ignore.Contains( gameId ) ) || ( ignoreDlc && Program.GameDB.IsDlc( gameId ) ) ) {
                            Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_SkippedProcessingGame, gameId );
                            continue;
                        }
                        if( gameNodePair.Value != null && gameNodePair.Value.ContainsKey( "tags" ) ) {
                            Category cat = null;
                            bool fav = false;
                            loadedGames++;
                            VdfFileNode tagsNode = gameNodePair.Value["tags"];
                            Dictionary<string, VdfFileNode> tagArray = tagsNode.NodeArray;
                            if( tagArray != null ) {
                                foreach( VdfFileNode tag in tagArray.Values ) {
                                    string tagName = tag.NodeString;
                                    if( tagName != null ) {
                                        if( tagName == "favorite" ) {
                                            fav = true;
                                        } else if( cat == null ) {
                                            cat = GetCategory( tagName );
                                        }
                                    }
                                }
                            }

                            if( !Games.ContainsKey( gameId ) ) {
                                Game newGame = new Game( gameId, string.Empty );
                                Games.Add( gameId, newGame );
                                newGame.Name = Program.GameDB.GetName( gameId );
                                Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_AddedNewGame, gameId, newGame.Name );
                            }
                            if( cat != null ) {
                                Games[gameId].Category = cat;
                            }
                            Games[gameId].Favorite = fav;
                            Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_ProcessedGame, gameId, ( cat == null ) ? "~none~" : cat.ToString(), fav );
                        }
                    }
                }
            }

            return loadedGames;
        }

        /// <summary>
        /// Writes category info for shortcut games to shortcuts.vdf config file for specified Steam user.
        /// </summary>
        /// <param name="SteamId">Identifier of Steam user to save information</param>
        /// <param name="discardMissing">If true, category information in shortcuts.vdf file is removed if game is not in Game list</param>
        private void SaveShortcutGames( long SteamId, bool discardMissing ) {
            string filePath = string.Format( Properties.Resources.ShortCutsFilePath, Settings.Instance().SteamPath, Profile.ID64toDirName( SteamId ) );
            Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_SavingSteamConfigFile, filePath );
            FileStream fStream = null;
            BinaryReader binReader = null;
            BinaryVdfFileNode dataRoot = null;
            try {
                fStream = new FileStream( filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite );
                binReader = new BinaryReader( fStream );

                dataRoot = BinaryVdfFileNode.Load( binReader );
            } catch( FileNotFoundException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorOpeningConfigFileParam, e.ToString() );
            } catch( IOException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_LoadingErrorSteamConfig, e.ToString() );
            }
            if( binReader != null )
                binReader.Close();
            if( fStream != null )
                fStream.Close();
            if( dataRoot != null ) {

                List<Game> gamesToSave = new List<Game>();
                foreach( int id in Games.Keys ) {
                    if( id < 0 ) gamesToSave.Add( Games[id] );
                }

                StringDictionary launchIds = new StringDictionary();
                LoadShortcutLaunchIds( SteamId, out launchIds );

                VdfFileNode appsNode = dataRoot.GetNodeAt( new string[] { "shortcuts" }, false );
                foreach( KeyValuePair<string, VdfFileNode> shortcutPair in appsNode.NodeArray ) {
                    VdfFileNode nodeGame = shortcutPair.Value;
                    int nodeId = -1;
                    int.TryParse( shortcutPair.Key, out nodeId );

                    // Currently matches based on name, not ID. Matching based on filepath  might be better, but requires extra storage
                    int matchingIndex = FindMatchingShortcut( nodeId, nodeGame, gamesToSave, launchIds );

                    if( matchingIndex >= 0 ) {
                        Game game = gamesToSave[matchingIndex];
                        gamesToSave.RemoveAt( matchingIndex );
                    
                        Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_AddingGameToConfigFile, game.Id );

                        VdfFileNode tagsNode = nodeGame.GetNodeAt( new string[] { "tags" }, true );
                        tagsNode.NodeArray.Clear();

                        int index = 0;
                        if( game.Category != null ) {
                            tagsNode.NodeArray.Add( index.ToString(), new BinaryVdfFileNode( game.Category.Name ) );
                            index++;
                        }
                        if( game.Favorite ) {
                            tagsNode.NodeArray.Add( index.ToString(), new BinaryVdfFileNode( "favorite" ) );
                        }
                    }
                    /* Don't think I really want the program to be removing shortcuts at all right now, but might add an option later specifically for removing them
                    else if( discardMissing ) {
                        Program.Logger.Write( LoggerLevel.Verbose, GlobalStrings.GameData_RemovingGameCategoryFromSteamConfig, shortcutId );
                        tagsNode.NodeArray.Clear();
                    }*/
                    //}
                }
                if( dataRoot.NodeType == ValueType.Array ) {
                    Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_SavingShortcutConfigFile, filePath );
                    BinaryWriter binWriter;
                    try {
                        fStream = new FileStream( filePath, FileMode.Truncate, FileAccess.ReadWrite, FileShare.ReadWrite );
                        binWriter = new BinaryWriter( fStream );
                        dataRoot.Save( binWriter );
                        binWriter.Close();
                        fStream.Close();
                    } catch( ArgumentException e ) {
                        Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorSavingSteamConfigFile, e.ToString() );
                        throw new ApplicationException( GlobalStrings.GameData_FailedToSaveSteamConfigBadPath, e );
                    } catch( IOException e ) {
                        Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorSavingSteamConfigFile, e.ToString() );
                        throw new ApplicationException( GlobalStrings.GameData_FailedToSaveSteamConfigFile + e.Message, e );
                    } catch( UnauthorizedAccessException e ) {
                        Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorSavingSteamConfigFile, e.ToString() );
                        throw new ApplicationException( GlobalStrings.GameData_AccessDeniedSteamConfigFile + e.Message, e );
                    }
                }
            }
            //}
        }

        /// <summary>
        /// Load launch IDs for external games from screenshots.vdf
        /// </summary>
        /// <param name="SteamId">Steam user identifier</param>
        /// <param name="shortcutLaunchIds">Found games listed as pairs of {gameName, gameId} </param>
        /// <returns>True if file was successfully loaded, false otherwise</returns>
        private bool LoadShortcutLaunchIds( long SteamId, out StringDictionary shortcutLaunchIds ) {
            bool result = false;
            string filePath = string.Format( Properties.Resources.ScreenshotsFilePath, Settings.Instance().SteamPath, Profile.ID64toDirName( SteamId ) );

            shortcutLaunchIds = new StringDictionary();

            StreamReader reader = null;
            try {
                reader = new StreamReader( filePath, false );
                TextVdfFileNode dataRoot = TextVdfFileNode.Load( reader, true );

                VdfFileNode appsNode = dataRoot.GetNodeAt( new string[] { "shortcutnames" }, false );

                foreach( KeyValuePair<string, VdfFileNode> shortcutPair in appsNode.NodeArray ) {
                    string launchId = shortcutPair.Key;
                    string gameName = (string)shortcutPair.Value.NodeData;
                    if( !shortcutLaunchIds.ContainsKey( gameName ) ) {
                        shortcutLaunchIds.Add( gameName, launchId );
                    }
                }
                result = true;
            } catch( FileNotFoundException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorOpeningConfigFileParam, e.ToString() );
            } catch( IOException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_LoadingErrorSteamConfig, e.ToString() );
            }

            if( reader != null ) {
                reader.Close();
            }

            return result;

        }

        /// <summary>
        /// Updates set of non-Steam games. Will remove any games that are currently in the list but not found in the Steam config.
        /// </summary>
        /// <param name="SteamId">The ID64 of the account to load shortcuts for</param>
        /// <param name="overwriteCategories">If true, overwrite categories for found games. If false, only load categories for games without a category already set.</param>
        /// <param name="newItems">Number of new items added</param>
        /// <param name="removedItems">Number of old items removed.</param>
        /// <returns>Total number of entries processed</returns>
        public int ImportNonSteamGames( long SteamId, bool overwriteCategories, out int newItems, out int removedItems ) {
            newItems = 0;
            removedItems = 0;
            if( SteamId <= 0 ) return 0;
            int loadedGames = 0;

            string filePath = string.Format( Properties.Resources.ShortCutsFilePath, Settings.Instance().SteamPath, Profile.ID64toDirName( SteamId ) );
            FileStream fStream = null;
            BinaryReader binReader = null;
            try {
                fStream = new FileStream( filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite );
                binReader = new BinaryReader( fStream );

                BinaryVdfFileNode dataRoot = BinaryVdfFileNode.Load( binReader );

                VdfFileNode shortcutsNode = dataRoot.GetNodeAt( new string[] { "shortcuts" }, false );

                if( shortcutsNode != null ) {
                    List<Game> oldShortcutGames = new List<Game>();
                    foreach( int id in Games.Keys ) {
                        if( id < 0 ) {
                            oldShortcutGames.Add( Games[id] );
                            Games.Remove( id );
                        }
                    }

                    StringDictionary launchIds = null;
                    bool launchIdsLoaded = LoadShortcutLaunchIds( SteamId, out launchIds );

                    foreach( KeyValuePair<string, VdfFileNode> shortcutPair in shortcutsNode.NodeArray ) {
                        VdfFileNode nodeGame = shortcutPair.Value;
                        VdfFileNode nodeName = nodeGame.GetNodeAt( new string[] { "appname" }, false );

                        string gameName = ( nodeName != null ) ? nodeName.NodeString : null;
                        int gameId = -1;
                        int.TryParse( shortcutPair.Key, out gameId );

                        bool success = IntegrateShortcut( gameId, nodeGame, oldShortcutGames, launchIds, ref newItems );
                        if( success ) loadedGames++;
                    }
                    removedItems = oldShortcutGames.Count;
                }
            } catch( FileNotFoundException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_ErrorOpeningConfigFileParam, e.ToString() );
            } catch( IOException e ) {
                Program.Logger.Write( LoggerLevel.Error, GlobalStrings.GameData_LoadingErrorSteamConfig, e.ToString() );
            } catch( ParseException e ) {
                Program.Logger.Write( LoggerLevel.Error, e.ToString() );
            } finally {
                if( binReader != null ) {
                    binReader.Close();
                }
                if( fStream != null ) {
                    fStream.Close();
                }
            }

            Program.Logger.Write( LoggerLevel.Info, GlobalStrings.GameData_IntegratedShortCuts, loadedGames, newItems, removedItems );

            return loadedGames;
        }

        /// <summary>
        /// Searches a list of games, looking for the one that matches the information in the shortcut node.
        /// Checks launch ID first, then checks a combination of name and ID, then just checks name.
        /// </summary>
        /// <param name="shortcutId">ID of the shortcut node</param>
        /// <param name="shortcutNode">Shotcut node itself</param>
        /// <param name="gamesToMatchAgainst">List of game objects to match against</param>
        /// <param name="shortcutLaunchIds">List of launch IDs referenced by name</param>
        /// <returns>The index of the matching game if found, -1 otherwise.</returns>
        private int FindMatchingShortcut( int shortcutId, VdfFileNode shortcutNode, List<Game> gamesToMatchAgainst, StringDictionary shortcutLaunchIds ) {
            VdfFileNode nodeName = shortcutNode.GetNodeAt( new string[] { "appname" }, false );
            string gameName = ( nodeName != null ) ? nodeName.NodeString : null;
            string launchId = shortcutLaunchIds[gameName];

            for( int i = 0; i < gamesToMatchAgainst.Count; i++ ) {
                if( gamesToMatchAgainst[i].LaunchString == launchId ) return i;
            }

            for( int i = 0; i < gamesToMatchAgainst.Count; i++ ) {
                if( gamesToMatchAgainst[i].Id == shortcutId && gamesToMatchAgainst[i].Name == gameName ) return i;
            }

            for( int i = 0; i < gamesToMatchAgainst.Count; i++ ) {
                if( gamesToMatchAgainst[i].Name == gameName ) return i;
            }

            return -1;
        }

        /// <summary>
        /// Adds a non-steam game to the gamelist.
        /// </summary>
        /// <param name="gameId">ID of the game in the steam config file</param>
        /// <param name="gameNode">Node for the game in the steam config file</param>
        /// <param name="oldShortcuts">List of un-matched non-steam games from the gamelist before the update</param>
        /// <param name="launchIds">Dictionary of launch ids (name:launchId)</param>
        /// <param name="newGames">Number of NEW games that have been added to the list</param>
        /// <param name="preferSteamCategories">If true, prefers to use the categories from the steam config if there is a conflict. If false, prefers to use the categories from the existing gamelist.</param>
        /// <returns>True if the game was successfully added</returns>
        private bool IntegrateShortcut( int gameId, VdfFileNode gameNode, List<Game> oldShortcuts, StringDictionary launchIds, ref int newGames, bool preferSteamCategories = true ) {
            VdfFileNode nodeName = gameNode.GetNodeAt( new string[] { "appname" }, false );
            string gameName = ( nodeName != null ) ? nodeName.NodeString : null;
            int newId = -gameId;

            if( Games.ContainsKey( newId ) ) {
                return false;
            }

            Game game = new Game( newId, gameName );
            Games.Add( newId, game );

            game.LaunchString = launchIds[gameName];

            int oldShortcutId = FindMatchingShortcut( gameId, gameNode, oldShortcuts, launchIds );
            bool oldCatSet = ( oldShortcutId != -1 ) && oldShortcuts[oldShortcutId].Category != null;
            if( oldShortcutId == -1 ) newGames++;
            
            VdfFileNode tagsNode = gameNode.GetNodeAt( new string[] { "tags" }, false );
            bool steamCatSet = ( tagsNode != null && tagsNode.NodeType == ValueType.Array && tagsNode.NodeArray.Count > 0 );
            
            //fill in categories
            if( steamCatSet && (preferSteamCategories || !oldCatSet) )  {
                foreach( KeyValuePair<string, VdfFileNode> tag in tagsNode.NodeArray) {
                    string tagName = tag.Value.NodeString;
                    if( tagName != null ) {
                        if( tagName == "favorite" ) {
                            game.Favorite = true;
                        } else {
                            game.Category = this.GetCategory( tagName );
                        }
                    }
                }

            } else {
                game.Category = oldShortcuts[oldShortcutId].Category;
                game.Favorite = oldShortcuts[oldShortcutId].Favorite;
            }

            if( oldShortcutId != -1 ) oldShortcuts.RemoveAt( oldShortcutId );
            return true;
        }

        #endregion
    }

    class ProfileAccessException : ApplicationException {
        public ProfileAccessException( string m ) : base( m ) { }
    }
}