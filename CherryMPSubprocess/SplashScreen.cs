using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CherryMPShared;

namespace CherryMP
{
    public partial class SplashScreen : Form
    {
        public SplashScreen()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MainBehaviour.EntryPoint();
        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }
    }
}
