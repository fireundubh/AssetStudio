using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class AnimatorOverrideController : NamedObject
    {
        public PPtr m_Controller;
        public PPtr[][] m_Clips;

        public AnimatorOverrideController(ObjectReader reader) : base(reader)
        {
            this.m_Controller = reader.ReadPPtr();

            int numOverrides = reader.ReadInt32();
            this.m_Clips = new PPtr[numOverrides][];

            for (var i = 0; i < numOverrides; i++)
            {
                this.m_Clips[i] = new PPtr[2];
                this.m_Clips[i][0] = reader.ReadPPtr();
                this.m_Clips[i][1] = reader.ReadPPtr();
            }
        }
    }
}