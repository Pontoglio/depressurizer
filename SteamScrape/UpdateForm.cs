﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace SteamScrape {
    public partial class UpdateForm : Form {

        object abortLock = new object();

        const int MAX_THREADS = 5;
        int runningThreads;

        Queue<int> jobs;
        GameDB games;

        int totalJobs;
        int jobsCompleted;

        System.DateTime start;

        bool _abort = false;
        bool Aborted {
            get {
                return _abort;
            }
            set {
                _abort = value;
            }
        }

        public UpdateForm( GameDB games, Queue<int> jobs ) {
            InitializeComponent();
            this.jobs = jobs;
            this.games = games;

            totalJobs = jobs.Count;
            jobsCompleted = 0;
        }

        GameDBEntry GetNextGame() {
            int gameId;
            lock( jobs ) {
                if( jobs.Count > 0 ) {
                    gameId = jobs.Dequeue();
                } else {
                    return null;
                }
            }
            GameDBEntry res = null;
            lock( games ) {
                res = games.Games[gameId];
            }
            return res;
        }

        /// <summary>
        /// Runs the next job in the queue, in a thread-safe manner. Aborts ASAP if the form is closed.
        /// </summary>
        /// <returns>True if a job was run, false otherwise</returns>
        bool RunNextJob() {
            if( Aborted ) return false;

            GameDBEntry game = GetNextGame();
            if( game == null ) {
                return false;
            }
            if( Aborted ) return false;

            string genre = null;
            AppType type = GameDBEntry.ScrapeStore( game.Id, out genre );

            // This lock is critical, as it makes sure that the abort check and the actual game update funtion essentially atomically with reference to form-closing.
            // If this isn't the case, the form could successfully close before this happens, but then it could still go through, and that's no good.
            lock( abortLock ) {
                if( !Aborted ) {
                    game.Type = type;
                    if( type == AppType.Game || type == AppType.DLC || type == AppType.IdRedirect ) {
                        game.Genre = genre;
                    }
                    
                    return true;
                } else {
                    return false;
                }
            }
        }


        delegate void SimpleDelegate();

        void RunJobs() {
            bool running = true;
            while( !Aborted && running ) {
                running = RunNextJob();
                if( !Aborted && running ) {
                    this.Invoke( new SimpleDelegate( JobCompleted ) );
                }
            }
            if( !Aborted ) {
                this.Invoke( new SimpleDelegate( EndThread ) );
            }

        }

        void EndThread() {
            if( !Aborted ) {
                runningThreads--;
                if( runningThreads <= 0 ) {
                    this.Close();
                }
            }
        }

        private void cmdStop_Click( object sender, EventArgs e ) {
            this.Close();
        }

        private void UpdateForm_FormClosing( object sender, FormClosingEventArgs e ) {
            lock( abortLock ) {
                Aborted = true;
            }
        }

        void JobCompleted() {
            jobsCompleted++;
            UpdateText();
        }

        void UpdateText() {
            double msElapsed = ( DateTime.Now - start ).TotalMilliseconds;
            double msPerItem = msElapsed / (double)jobsCompleted;
            double msRemaining = msPerItem * ( totalJobs - jobsCompleted );
            TimeSpan timeRemaining = TimeSpan.FromMilliseconds( msRemaining );

            lblText.Text = string.Format( "Updating...{0}/{1} complete.", jobsCompleted, totalJobs );

            StringBuilder remainingTimeString = new StringBuilder( "Time Remaining: " );
            if( timeRemaining.TotalMinutes < 1.0 ) {
                remainingTimeString.Append( "< 1 minute" );
            } else {
                double hours = timeRemaining.TotalHours;
                if( hours >= 1.0 ) {
                    remainingTimeString.Append( string.Format("{0:F0}h", hours) );
                }
                remainingTimeString.Append( string.Format( "{0:D2}m", timeRemaining.Minutes ) );
            }
            lblTime.Text = remainingTimeString.ToString();
        }

        private void UpdateForm_Load( object sender, EventArgs e ) {
            int threadsToRun = Math.Min( MAX_THREADS, jobs.Count );
            start = DateTime.Now;
            for( int i = 0; i < MAX_THREADS; i++ ) {
                Thread t = new Thread( new ThreadStart( RunJobs ) );
                t.Start();
                runningThreads++;
            }
            UpdateText();
        }
    }
}
