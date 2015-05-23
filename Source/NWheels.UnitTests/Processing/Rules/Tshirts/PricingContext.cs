﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NWheels.UnitTests.Processing.Rules.Tshirts
{
    public interface ICollectionRuleSystemContext<T> : IEnumerable<T>
    {
        T Current { get; set; }
    }


    public class PricingContext : ICollectionRuleSystemContext<ITshirtEntity>
    {
        public PricingContext(params ITshirtEntity[] products)
        {
            this.Products = products.ToList();
            this.PriceLines = new List<PriceLine>();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public IEnumerator<ITshirtEntity> GetEnumerator()
        {
            return Products.GetEnumerator();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------
        
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Products.GetEnumerator();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public ITshirtEntity Current { get; set; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public List<ITshirtEntity> Products { get; private set; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public List<PriceLine> PriceLines { get; private set; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public class PriceLine
        {
            public Money Price { get; set; }
            public string Description { get; set; }
        }
    }
}
