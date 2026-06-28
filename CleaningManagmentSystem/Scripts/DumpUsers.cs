using System;
using System.Data;
using MySql.Data.MySqlClient;

class Program
{
    static void Main()
    {
        string connectionString = "Server=127.0.0.1;Port=3306;Database=yeka_cleaning;User=root;Password=;";
        using (var connection = new MySqlConnection(connectionString))
        {
            connection.Open();
            var command = new MySqlCommand("SELECT id, email, role, password FROM users;", connection);
            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine("ID | Email | Role | Password");
                Console.WriteLine("-------------------------------------------------");
                while (reader.Read())
                {
                    Console.WriteLine($"{reader["id"]} | {reader["email"]} | {reader["role"]} | {reader["password"]}");
                }
            }
        }
    }
}
