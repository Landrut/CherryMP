﻿using CherryMPServer.Constant;
using CherryMPShared;

namespace CherryMPServer
{
    public class TextLabel : Entity
    {
        internal TextLabel(ServerAPI father, NetHandle handle) : base(father, handle)
        {
        }

        #region Properties

        public string text
        {
            get { return Base.getTextLabelText(this); }
            set { Base.setTextLabelText(this, value); }
        }

        public Color color
        {
            get { return Base.getTextLabelColor(this); }
            set { Base.setTextLabelColor(this, value.red, value.green, value.blue,value.alpha); }
        }

        public bool seethrough
        {
            get { return Base.getTextLabelSeethrough(this); }
            set { Base.setTextLabelSeethrough(this, value); }
        }

        public float range
        {
            get { return Base.getTextLabelRange(this); }
            set { Base.setTextLabelRange(this, value); }
        }

        #endregion

        #region Methods
        #endregion
    }
}