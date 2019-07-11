using Db.DataAccess.DataSet;
using LinqToSQL3.Example.DataAccess;
using LinqToSQL3NetCore.Example.DataAccess;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace LinqToSQL3NetCore.Example
{
    public class TestInsertUpdateDelete
    {
        DbContext dbContext;

        public TestInsertUpdateDelete(DbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        #region Utils
        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        #endregion

        #region Create Entities
        public User CreateUser()
        {
            var firstName = RandomString(5);
            var lastName = RandomString(6);
            return new User()
            {
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName}.{lastName}@domain.com"
            };
        }

        public Order CreateOrder(User user = null)
        {
            return new Order()
            {
                InsertTimestamp = DateTime.Now,
                User = user
            };
        }

        public Item CreateItem(Order order = null)
        {
            return new Item()
            {
                Price = random.Next(1, 100),
                Order = order
            };
        }

        public List<Item> CreateItems(int count, Order order = null)
        {
            var result = new List<Item>(capacity: count);
            for(var i = 0; i < count; i++)
            {
                result.Add(CreateItem(order));
            }
            return result;
        }

        public User CreateUserOrderItems(int itemCount)
        {
            var user = CreateUser();
            var order = CreateOrder(user);
            var items = CreateItems(itemCount, order);
            return user;
        }

        #endregion

        public void RunTest1()
        {
            var user = CreateUserOrderItems(10);
            dbContext.InsertOnSubmit(user);
            dbContext.SubmitChanges();
        }

        public void RunTest2()
        {
            var user = CreateUser();
            dbContext.InsertOnSubmit(user);
            dbContext.SubmitChanges();
            var order = CreateOrder(user);
            dbContext.SubmitChanges();
            order.InsertTimestamp = DateTime.Now.AddDays(1);

            user.FirstName = "MFirstName";
            user.LastName = "MLastName";
            user.Email = $"{user.FirstName}.{user.LastName}@domain.com";

            dbContext.DeleteOnSubmit(order);

            var insertUserOrderItems = CreateUserOrderItems(10);
            dbContext.InsertOnSubmit(insertUserOrderItems);
            dbContext.SubmitChanges();

            // cleanup
            dbContext.DeleteOnSubmit(user);
            dbContext.DeleteOnSubmit(insertUserOrderItems);
            dbContext.DeleteAllOnSubmit(insertUserOrderItems.Orders);
            dbContext.DeleteAllOnSubmit(insertUserOrderItems.Orders.SelectMany(o => o.Items));
            dbContext.SubmitChanges();
        }
    }
}
