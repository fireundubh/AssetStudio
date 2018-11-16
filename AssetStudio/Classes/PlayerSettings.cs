using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class PlayerSettings : Object
    {
        public string companyName;
        public string productName;

        public PlayerSettings(ObjectReader reader) : base(reader)
        {
            if (this.version[0] > 5 || this.version[0] == 5 && this.version[1] >= 4) //5.4.0 nad up
            {
                byte[] productGUID = this.reader.ReadBytes(16);
            }

            bool AndroidProfiler = this.reader.ReadBoolean();

            //bool AndroidFilterTouchesWhenObscured 2017.2 and up
            //bool AndroidEnableSustainedPerformanceMode 2018 and up

            this.reader.AlignStream(4);

            int defaultScreenOrientation = this.reader.ReadInt32();
            int targetDevice = this.reader.ReadInt32();

            if (this.version[0] < 5 || this.version[0] == 5 && this.version[1] < 3) //5.3 down
            {
                if (this.version[0] < 5) //5.0 down
                {
                    int targetPlatform = this.reader.ReadInt32(); //4.0 and up targetGlesGraphics

                    if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 6) //4.6 and up
                    {
                        int targetIOSGraphics = this.reader.ReadInt32();
                    }
                }
                int targetResolution = this.reader.ReadInt32();
            }
            else
            {
                bool useOnDemandResources = this.reader.ReadBoolean();
                this.reader.AlignStream(4);
            }

            if (this.version[0] > 3 || this.version[0] == 3 && this.version[1] >= 5) //3.5 and up
            {
                int accelerometerFrequency = this.reader.ReadInt32();
            }

            this.companyName = this.reader.ReadAlignedString();
            this.productName = this.reader.ReadAlignedString();
        }
    }
}