using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class Animator : Behaviour
    {
        public PPtr m_Avatar;
        public PPtr m_Controller;
        public bool m_HasTransformHierarchy;

        public Animator(ObjectReader reader) : base(reader)
        {
            this.m_Avatar = reader.ReadPPtr();
            this.m_Controller = reader.ReadPPtr();
            int m_CullingMode = reader.ReadInt32();

            if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 5) //4.5 and up
            {
                int m_UpdateMode = reader.ReadInt32();
            }

            bool m_ApplyRootMotion = reader.ReadBoolean();

            if (this.version[0] == 4 && this.version[1] >= 5) //4.5 and up - 5.0 down
            {
                reader.AlignStream();
            }

            if (this.version[0] >= 5) //5.0 and up
            {
                bool m_LinearVelocityBlending = reader.ReadBoolean();
                reader.AlignStream();
            }

            if (this.version[0] < 4 || this.version[0] == 4 && this.version[1] < 5) //4.5 down
            {
                bool m_AnimatePhysics = reader.ReadBoolean();
            }

            if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 3) //4.3 and up
            {
                this.m_HasTransformHierarchy = reader.ReadBoolean();
            }

            if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 5) //4.5 and up
            {
                bool m_AllowConstantClipSamplingOptimization = reader.ReadBoolean();
            }

            if (this.version[0] >= 5 && this.version[0] < 2018) //5.0 and up - 2018 down
            {
                reader.AlignStream();
            }

            if (this.version[0] >= 2018) //2018 and up
            {
                bool m_KeepAnimatorControllerStateOnDisable = reader.ReadBoolean();
                reader.AlignStream();
            }
        }
    }
}