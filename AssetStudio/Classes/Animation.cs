using System.Collections.Generic;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class Animation : Behaviour
    {
        public List<PPtr> m_Animations;

        public Animation(ObjectReader reader) : base(reader)
        {
            PPtr m_Animation = reader.ReadPPtr();
            int numAnimations = reader.ReadInt32();

            this.m_Animations = new List<PPtr>(numAnimations);

            for (var i = 0; i < numAnimations; i++)
            {
                this.m_Animations.Add(reader.ReadPPtr());
            }
        }
    }
}