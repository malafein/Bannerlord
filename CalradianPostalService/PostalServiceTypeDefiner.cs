using CalradianPostalService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace CalradianPostalService
{
    class PostalServiceTypeDefiner : SaveableTypeDefiner
    {
        public PostalServiceTypeDefiner() : base(650000)
        {
        }

        protected override void DefineClassTypes()
        {
            base.AddClassDefinition(typeof(MissiveFriendly), 1);
            base.AddClassDefinition(typeof(MissiveThreat), 2);
            base.AddClassDefinition(typeof(MissiveCommand), 3);
        }

        protected override void DefineContainerDefinitions()
        {
            base.ConstructContainerDefinition(typeof(List<IMissive>));
        }

        protected override void DefineGenericClassDefinitions()
        {
            base.ConstructGenericClassDefinition(typeof(MBObjectManager.ObjectTypeRecord<MissiveBase>));
        }

        protected override void DefineInterfaceTypes()
        {
            base.AddInterfaceDefinition(typeof(IMissive), 5001);
        }
    }
}
