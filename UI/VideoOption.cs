using System;

namespace BelmontsCurseMasterOptimizer.UI
{
    public class VideoOption
    {
        public string Path;
        public string Key;
        public string Title;
        public (string key, string text, int value)[] Choices;
        public Func<int> Default;
        public Action<int> Apply;

        internal EnumSetting Setting;
        internal int Applied = int.MinValue;
    }
}
