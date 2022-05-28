using System;
using System.Collections.Generic;

namespace ODataDemo.Db
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
        public ICollection<Role> Roles { get; set; }
    }

    public class Role
    {
        public int Id { get; set; }
        public string Caption { get; set; }
        public string Credential { get; set; }
    }
}
