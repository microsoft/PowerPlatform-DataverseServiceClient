using FluentAssertions;
using System;
using Microsoft.Xrm.Sdk;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrmSdk;

namespace LivePackageTestsConsole
{
    public class CUDTest
    {
        public void RunTest()
        {
            Console.WriteLine("Starting CUDTest Flow");

            var client = Auth.CreateClient();
            client.IsReady.Should().BeTrue();

            Console.WriteLine("Creating Account");

            Entity acct = new Entity("account");
            acct.Attributes["name"] = "testaccount";

            Guid id = client.Create(acct);

            Console.WriteLine("Updating Account");

            Entity acct2 = new Entity("account"); // changing to force a 'new' situation
            acct2.Id = id;
            acct2.Attributes["name"] = "testaccount2";

            client.Update(acct2);

            Console.WriteLine("Deleting Account");
            client.Delete("account", id); 
        }

        public void RunTest2()
        {
            Console.WriteLine("Starting CUDTest Flow - OrganziationContext");

            var client = Auth.CreateClient();
            client.IsReady.Should().BeTrue();

            using (DvServiceContext svcCtx = new DvServiceContext(client))
            {
                //svcCtx.MergeOption = Microsoft.Xrm.Sdk.Client.MergeOption.NoTracking; // So as to not keep cached copies while working on test cases.
                Console.WriteLine("Creating Account");

                Account acct = new Account();
                acct.Name = "testaccount";
                svcCtx.AddObject(acct);
                svcCtx.SaveChanges();

                Guid id = acct.Id;

                Console.WriteLine("Query Account");
                var aQ = (from a1 in svcCtx.AccountSet
                          where a1.Name.Equals("testaccount")
                          select a1);

                if (aQ != null )
                    Console.WriteLine($"Found Account by Name {aQ.FirstOrDefault().Name}");

                Console.WriteLine("Updating Account");

                Entity acct2 = new Entity("account"); // changing to force a 'new' situation
                acct2.Id = id;
                acct2.Attributes["name"] = "testaccount2";
                client.Update(acct2);

                Console.WriteLine("Deleting Account");
                client.Delete("account", id);
            }
        }
    }
}
