﻿using System;
using System.Collections.Generic;
using System.Linq;
using EasyNetQ;
using Messages;

namespace Warehouse
{
    public class Warehouse
    {
        private string country;
        private int id;
        private IEnumerable<Product> products = null;
        private IBus bus = null;

        public Warehouse(int id, string country, IEnumerable<Product> products)
        {
            this.id = id;
            this.country = country;
            this.products = products;
        }

        public void Start()
        {
            // Password is not valid anymore, still useful to look at how the string should look like
            using (bus = RabbitHutch.CreateBus("host=goose.rmq2.cloudamqp.com;virtualHost=mldigrlk;username=mldigrlk;password=zotjYObsAkeTHWQcJxnyrgXrk2RHE7FJ;persistentMessages=false"))
            {
                // Listen for order request messages.
                bus.PubSub.Subscribe<OrderRequestMessage>("warehouse" + id.ToString(), HandleOrderRequestMessage);

                lock (this)
                {
                    Console.WriteLine("Warehouse " + id + ": Listening for order requests\n");
                }

                // Block this thread so that the warehouse instance will not exit.
                Console.ReadLine();
            }
        }


        void HandleOrderRequestMessage(OrderRequestMessage message)
        {
            lock (this)
            {
                Console.WriteLine("Warehouse " + id + ": Order request received for order " + message.OrderId + "." + " Order country: " + message.Country);
            }

            int daysForDelivery = country == message.Country ? 2 : 10;
            decimal shippingCharge = country == message.Country ? 5 : 10;

            Product requestedProduct =
                products.FirstOrDefault(p => p.ProductId == message.ProductId);

            if (requestedProduct != null)
            {
                OrderReplyMessage replyMessage = new OrderReplyMessage
                {
                    WarehouseId = id,
                    OrderId = message.OrderId,
                    ItemsInStock = requestedProduct.ItemsInStock,
                    DaysForDelivery = daysForDelivery,
                    ShippingCharge = shippingCharge
                };

                // Send the reply message to the Retailer.
                bus.SendReceive.Send("warehouseToRetailerQueue", replyMessage);

                lock (this)
                {
                    Console.WriteLine("Warehouse " + id + ": Reply for order " + 
                    message.OrderId + " sent back to retailer.");
                }
            }
        }

    }
}
