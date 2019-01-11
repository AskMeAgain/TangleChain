﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using TangleChainIXI.Interfaces;
using TangleChainIXI.Smartcontracts;
using TangleChainIXI.Smartcontracts.Classes;

namespace TangleChainIXI.Classes
{

    public class DataBase
    {

        private SQLiteConnection Db { get; set; }
        public string CoinName { get; set; }
        public bool ExistedBefore { get; set; }

        private ChainSettings cSett;
        public ChainSettings ChainSettings {
            get => cSett ?? GetChainSettingsFromDB();
            set => cSett = value;
        }

        internal DataBase(string name)
        {

            //if the db exists we set this flag
            ExistedBefore = Exists(name) ? true : false;

            CoinName = name;
            string path = IXISettings.DataBasePath;

            //first we create file structure
            if (!Directory.Exists($@"{path}{name}\") || !File.Exists($@"{path}{name}\chain.db"))
            {
                Directory.CreateDirectory($@"{path}{name}\");

                string sql =
                    "CREATE TABLE IF NOT EXISTS Block (Height INT PRIMARY KEY, Nonce INT NOT NULL, Time LONG NOT NULL, Hash CHAR(20) NOT NULL, " +
                    "NextAddress CHAR(81) NOT NULL, PublicKey CHAR(81) NOT NULL, SendTo CHAR(81) NOT NULL, Difficulty INT NOT NULL);";

                string sql2 =
                    "CREATE TABLE IF NOT EXISTS Transactions (ID INTEGER PRIMARY KEY AUTOINCREMENT, Hash CHAR(81), Time LONG, _From CHAR(81), Signature CHAR(81)," +
                    "Mode INT,BlockID INT ,MinerReward INT NOT NULL,PoolHeight INT, FOREIGN KEY(BlockID) REFERENCES Block(Height) ON DELETE CASCADE);";

                string sql3 =
                    "CREATE TABLE IF NOT EXISTS Data (ID INTEGER PRIMARY KEY AUTOINCREMENT, _ArrayIndex INT NOT NULL, " +
                    "Data CHAR, TransID ,FOREIGN KEY (TransID) REFERENCES Transactions(ID) ON DELETE CASCADE);";

                string sql4 =
                    "CREATE TABLE IF NOT EXISTS Output (ID INTEGER PRIMARY KEY AUTOINCREMENT, _Values INT NOT NULL,_ArrayIndex INT NOT NULL, " +
                    "Receiver CHAR, TransID ID NOT NULL,FOREIGN KEY(TransID) REFERENCES Transactions(ID) ON DELETE CASCADE);";

                string sql5 =
                    "CREATE TABLE IF NOT EXISTS Smartcontracts (ID INTEGER PRIMARY KEY AUTOINCREMENT, Name CHAR NOT NULL, Hash CHAR NOT NULL," +
                    " Balance INT NOT NULL, Code CHAR NOT NULL, _FROM CHAR(81) NOT NULL, Signature CHAR NOT NULL, Fee INT NOT NULL" +
                    ", SendTo CHAR(81) NOT NULL,ReceivingAddress CHAR(81) NOT NULL ,BlockID INT ,PoolHeight INT ,FOREIGN KEY(BlockID) REFERENCES Block(Height) ON DELETE CASCADE);";

                string sql6 =
                    "CREATE TABLE IF NOT EXISTS Variables (ID INTEGER PRIMARY KEY AUTOINCREMENT,Name CHAR, Value CHAR, SmartID INT,FOREIGN KEY(SmartID) REFERENCES" +
                    " Smartcontracts(ID) ON DELETE CASCADE);";

                NoQuerySQL(sql);
                NoQuerySQL(sql2);
                NoQuerySQL(sql3);
                NoQuerySQL(sql4);
                NoQuerySQL(sql5);
                NoQuerySQL(sql6);

            }

        }

        public void DeleteBlock(long height)
        {

            //delete block
            string sql = $"PRAGMA foreign_keys = ON;DELETE FROM Block WHERE Height={height}";

            NoQuerySQL(sql);
        }

        public static bool Exists(string name)
        {
            return File.Exists($@"{IXISettings.DataBasePath}{name}\chain.db");
        }

        #region Helper Helper Helper Helper Helper Helper Helper Helper Helper Helper Helper Helper 

        private void AddSmartcontract(Smartcontract smart, long? blockID, long? poolHeight)
        {
            long SmartID = -1;

            string insertPool = "INSERT INTO Smartcontracts (Name, Hash, Balance, Code, _FROM, Signature, Fee, SendTo, ReceivingAddress, PoolHeight, BlockID) " +
                                $"SELECT'{smart.Name}', '{smart.Hash}', {smart.Balance}, '{smart.Code}', '{smart.From}','{smart.Signature}',{smart.TransactionFee},'{smart.SendTo}','{smart.ReceivingAddress}'," +
                                $" {IsNull(poolHeight)},{IsNull(blockID)}" +
                                $" WHERE NOT EXISTS (SELECT 1 FROM Smartcontracts WHERE ReceivingAddress='{smart.ReceivingAddress}'); SELECT last_insert_rowid();"; ;

            //Case 1: insert a transpool smartcontract
            if (poolHeight != null)
            {

                using (SQLiteDataReader reader = QuerySQL(insertPool))
                {
                    reader.Read();
                    SmartID = (long)reader[0];
                }

                //ADD DATA OF SMARTCONTRACT
                StoreSmartcontractData(smart, SmartID);

            }

            //Case 2: insert a smart from block:
            if (blockID != null)
            {

                //if normal smartcontract is already there because of it was included in transpool, we need to update it first.
                string sql = $"UPDATE Smartcontracts SET ID={blockID}, PoolHeight=NULL WHERE ReceivingAddress='{smart.ReceivingAddress}' AND PoolHeight IS NOT NULL;";

                int numOfAffected = NoQuerySQL(sql);

                //means we didnt had a smartcontract before
                if (numOfAffected == 0)
                {
                    using (SQLiteDataReader reader = QuerySQL(insertPool))
                    {
                        reader.Read();
                        SmartID = (long)reader[0];
                    }

                    StoreSmartcontractData(smart, SmartID);
                }
            }
        }

        private void AddTransaction(Transaction trans, long? blockID, long? poolHeight)
        {
            //data
            long TransID = -1;

            string insertPool = "INSERT INTO Transactions (Hash, Time, _FROM, Signature, Mode, BlockID, MinerReward, PoolHeight) " +
                                $"SELECT'{trans.Hash}', {trans.Time}, '{trans.From}', '{trans.Signature}', {trans.Mode}, {IsNull(blockID)}, {trans.ComputeMinerReward()}, {IsNull(poolHeight)}" +
                                $" WHERE NOT EXISTS (SELECT 1 FROM Transactions WHERE Hash='{trans.Hash}' AND Time={trans.Time}); SELECT last_insert_rowid();";

            //Case 1: insert a transpool transaction
            if (poolHeight != null)
            {

                using (SQLiteDataReader reader = QuerySQL(insertPool))
                {
                    reader.Read();
                    TransID = (long)reader[0];
                }

                StoreTransactionData(trans, TransID);

            }

            //Case 2: insert a normal trans from block:
            if (blockID != null)
            {

                //if normal trans is already there because of it was included in transpool, we need to update it first.
                string sql = $"UPDATE Transactions SET BlockID={blockID}, PoolHeight=NULL WHERE Hash='{trans.Hash}' AND Time={trans.Time} AND PoolHeight IS NOT NULL;";

                int numOfAffected = NoQuerySQL(sql);

                //means we didnt update anything and just added it straight to db. So we have to add it normally
                if (numOfAffected == 0)
                {
                    using (SQLiteDataReader reader = QuerySQL(insertPool))
                    {
                        reader.Read();
                        TransID = (long)reader[0];
                    }

                    //output & data
                    StoreTransactionData(trans, TransID);

                }

                //if transaction triggers smartcontract
                if (trans.Mode == 2 && trans.OutputReceiver.Count == 1)
                {

                    Smartcontract smart = GetSmartcontract(trans.OutputReceiver[0]);

                    //if the transaction has a dead end, nothing happens, but money is lost
                    if (smart != null)
                    {

                        Computer comp = new Computer(smart);

                        //the smartcontract could be buggy or the transaction could be not correctly calling the smartcontract
                        try
                        {
                            var result = comp.Run(trans);
                            smart.Code.Variables = comp.GetCompleteState();

                            //we need to check if the result is correct and spendable:
                            //we include this handmade transaction in our DB if true
                            if (GetBalance(smart.ReceivingAddress) > result.ComputeOutgoingValues())
                            {
                                AddTransaction(result, blockID, null);
                                UpdateSmartcontract(smart);
                            }

                        }
                        catch (Exception)
                        {
                            //nothing happens... you just lost money
                        }
                    }
                }
            }
        }

        private void AddBlock(Block block)
        {

            //no update when genesis block because of concurrency stuff (hack)
            if (block.Height == 0 && GetBlock(block.Height) != null)
                return;


            //first check if block already exists in db in a different version
            Block checkBlock = GetBlock(block.Height);
            if (checkBlock != null && !checkBlock.Hash.Equals(block.Hash))
            {
                DeleteBlock(block.Height);
            }

            //if block doesnt exists we add
            if (GetBlock(block.Height) == null)
            {
                string sql = $"INSERT INTO Block (Height, Nonce, Time, Hash, NextAddress, PublicKey, SendTo, Difficulty) " +
                             $"VALUES ({block.Height},{block.Nonce},{block.Time},'{block.Hash}','{block.NextAddress}','{block.Owner}','{block.SendTo}', {block.Difficulty.ToString()});";

                NoQuerySQL(sql);

                //add smartcontracts
                if (block.SmartcontractHashes != null && block.SmartcontractHashes.Count > 0)
                {
                    var smartList = Core.GetAllFromBlock<Smartcontract>(block);
                    smartList?.ForEach(s => AddSmartcontract(s, block.Height, null));
                }

                //add transactions!
                if (block.TransactionHashes != null && block.TransactionHashes.Count > 0)
                {
                    var transList = Core.GetAllFromBlock<Transaction>(block);

                    if (transList != null)
                        Add(transList, block.Height);

                    if (block.Height == 0)
                    {
                        //we set settings too
                        ChainSettings = GetChainSettingsFromDB();
                    }
                }
            }
        }

        private void StoreSmartcontractData(Smartcontract smart, long SmartID)
        {
            var state = smart.Code.Variables;

            foreach (var key in state.Keys)
            {
                string updateVars = $"INSERT INTO Variables (Name,Value,SmartID) VALUES ('{key}','{state[key].GetValueAs<string>()}',{SmartID});";
                NoQuerySQL(updateVars);
            }
        }

        private void StoreTransactionData(Transaction trans, long TransID)
        {
            //add data too
            for (int i = 0; i < trans.Data.Count; i++)
            {

                string sql2 = $"INSERT INTO Data (_ArrayIndex, Data, TransID) VALUES({i},'{trans.Data[i]}',{TransID});";

                NoQuerySQL(sql2);
            }

            //add receivers + output
            for (int i = 0; i < trans.OutputReceiver.Count; i++)
            {

                string sql2 = $"INSERT INTO Output (_Values, _ArrayIndex, Receiver, TransID) VALUES({trans.OutputValue[i]},{i},'{trans.OutputReceiver[i]}',{TransID});";

                NoQuerySQL(sql2);
            }
        }

        private List<string> GetTransactionData(long id)
        {

            //i keep the structure here because data could be zero and i need to correctly setup everything

            SQLiteCommand command = new SQLiteCommand(Db);

            var list = new List<string>();

            string sql = $"SELECT * FROM Data WHERE TransID={id} ORDER BY _ArrayIndex;";

            command.CommandText = sql;

            using (SQLiteDataReader reader = command.ExecuteReader())
            {

                if (!reader.Read())
                    return null;

                while (true)
                {

                    list.Add((string)reader[2]);

                    if (!reader.Read())
                        break;
                }
            }

            return list;
        }

        private (List<int>, List<string>) GetTransactionOutput(long id)
        {
            //i keep the structure here because data could be zero and i need to correctly setup everything

            SQLiteCommand command = new SQLiteCommand(Db);

            var listReceiver = new List<string>();
            var listValue = new List<int>();

            string sql = $"SELECT _Values,Receiver FROM Output WHERE TransID={id};";

            command.CommandText = sql;

            using (SQLiteDataReader reader = command.ExecuteReader())
            {

                if (!reader.Read())
                {
                    return (null, null);
                }

                while (true)
                {

                    listValue.Add((int)reader[0]);
                    listReceiver.Add((string)reader[1]);

                    if (!reader.Read())
                        break;
                }

            }

            return (listValue, listReceiver);
        }

        private List<Smartcontract> GetSmartcontractsFromTransPool(long poolHeight, int num)
        {
            //get normal data
            string sql = $"SELECT ReceivingAddress FROM Smartcontracts WHERE PoolHeight={poolHeight} ORDER BY Fee DESC LIMIT {num};";

            var smartList = new List<Smartcontract>();

            using (SQLiteDataReader reader = QuerySQL(sql))
            {
                for (int i = 0; i < num; i++)
                {

                    if (!reader.Read())
                        break;

                    string receivingAddr = (string)reader[0];

                    Smartcontract smart = GetSmartcontract(receivingAddr);

                    smartList.Add(smart);

                }
            }

            return smartList;
        }

        private List<Transaction> GetTransactionsFromTransPool(long height, int num)
        {

            //get normal data
            string sql = $"SELECT * FROM Transactions WHERE PoolHeight={height} ORDER BY MinerReward DESC LIMIT {num};";

            var transList = new List<Transaction>();

            using (SQLiteDataReader reader = QuerySQL(sql))
            {
                for (int i = 0; i < num; i++)
                {

                    if (!reader.Read())
                        break;

                    long ID = (long)reader[0];
                    var output = GetTransactionOutput(ID);

                    Transaction trans = new Transaction(reader, output.Item1, output.Item2, GetTransactionData(ID))
                    {
                        SendTo = Utils.GetTransactionPoolAddress(height, CoinName)
                    };

                    transList.Add(trans);

                }
            }

            return transList;
        }

        #endregion

        #region Set Set Set Set Set Set Set Set Set Set Set Set Set Set Set 

        public void Add<T>(List<T> obj, long? BlockHeight = null, long? poolHeight = null) where T : IDownloadable
        {
            obj.ForEach(x => Add(x, BlockHeight, poolHeight));
        }

        public void Add<T>(T obj, long? BlockHeight = null, long? poolHeight = null) where T : IDownloadable
        {

            if (typeof(T) == typeof(Block))
            {
                AddBlock((Block)(object)obj);
            }
            else if (typeof(T) == typeof(Transaction))
            {
                AddTransaction((Transaction)(object)obj, BlockHeight, poolHeight);
            }
            else if (typeof(T) == typeof(Smartcontract))
            {
                AddSmartcontract((Smartcontract)(object)obj, BlockHeight, poolHeight);
            }
            else
            {
                throw new ArgumentException("WRONG TYPE SOMEHOW THIS SHOULD NEVER APPEAR!");
            }
        }

        public void UpdateSmartcontract(Smartcontract smart)
        {
            //get smart id first
            long id = GetSmartcontractID(smart.ReceivingAddress) ?? throw new ArgumentException("Smartcontract with the given receiving address doesnt exist");

            //update the balance:
            long balance = GetBalance(smart.ReceivingAddress);

            string updateBalance = $"UPDATE Smartcontracts SET Balance={balance} WHERE ID={id};";
            NoQuerySQL(updateBalance);

            //update the states:
            var state = smart.Code.Variables;
            foreach (var key in state.Keys)
            {
                string updateVars =
                    $"UPDATE Variables SET Value='{state[key].GetValueAs<string>()}' WHERE  ID={id} AND Name='{key}';";
                NoQuerySQL(updateVars);
            }
        }

        #endregion

        #region Get Get Get Get Get Get Get Get Get Get Get Get Get 

        public long? GetSmartcontractID(string receivingAddr)
        {
            string query = $"SELECT ID FROM Smartcontracts WHERE ReceivingAddress='{receivingAddr}';";

            using (SQLiteDataReader reader = QuerySQL(query))
            {
                if (!reader.Read())
                    return null;

                return (long)reader[0];
            }
        }

        public Block GetBlock(long height)
        {
            Block block = null;

            //the normal block!
            string sql = $"SELECT * FROM Block WHERE Height={height}";

            using (SQLiteDataReader reader = QuerySQL(sql))
            {

                if (!reader.Read())
                {
                    return null;
                }

                block = new Block(reader, CoinName);
            }

            //transactions!
            string sqlTrans = $"SELECT Hash FROM Transactions WHERE BlockID={height}";

            using (SQLiteDataReader reader = QuerySQL(sqlTrans))
            {
                var transList = new List<string>();

                while (reader.Read())
                {
                    transList.Add((string)reader[0]);
                }

                block.Add<Transaction>(transList);
            }

            //smartcontracts
            string sqlSmart = $"SELECT Hash FROM Smartcontracts WHERE BlockID={height}";
            using (SQLiteDataReader reader = QuerySQL(sqlSmart))
            {
                var smartList = new List<string>();

                while (reader.Read())
                {
                    smartList.Add((string)reader[0]);
                }

                block.Add<Smartcontract>(smartList);
            }

            return block;

        }

        public Smartcontract GetSmartcontract(string receivingAddr)
        {

            string sql = $"SELECT * FROM Smartcontracts WHERE ReceivingAddress='{receivingAddr}';";

            using (SQLiteDataReader reader = QuerySQL(sql))
            {

                if (!reader.Read())
                {
                    return null;
                }

                Smartcontract smart = new Smartcontract(reader);

                //we also need to add the variables:
                //using(SQLiteDataReader reader = QuerySQL($"SELECT * FROM Variables WHERE "))
                smart.Code.Variables = GetVariablesFromDB((long)reader[0]);
                return smart;
            }
        }

        public Transaction GetTransaction(string hash, long height)
        {

            //get normal data
            string sql = $"SELECT * FROM Transactions WHERE Hash='{hash}' AND BlockID='{height}'";

            using (SQLiteDataReader reader = QuerySQL(sql))
            {

                if (!reader.Read())
                    return null;

                long ID = (long)reader[0];
                var output = GetTransactionOutput(ID);

                Transaction trans = new Transaction(reader, output.Item1, output.Item2, GetTransactionData(ID))
                {
                    SendTo = Utils.GetTransactionPoolAddress(height, CoinName)
                };

                return trans;
            }
        }

        public List<T> GetFromTransPool<T>(long poolHeight, int num) where T : ISignable
        {

            if (typeof(T) == typeof(Transaction))
            {
                return GetTransactionsFromTransPool(poolHeight, num).Cast<T>().ToList();
            }

            if (typeof(T) == typeof(Smartcontract))
            {
                return GetSmartcontractsFromTransPool(poolHeight, num).Cast<T>().ToList();
            }

            throw new ArgumentException("THIS SHOULD NEVER APPEAR!");

        }

        public Block GetLatestBlock()
        {

            //we first get highest ID
            using (SQLiteDataReader reader = QuerySQL($"SELECT IFNULL(MAX(Height),0) FROM Block"))
            {

                if (!reader.Read())
                    return null;

                return GetBlock((long)reader[0]);
            }
        }

        public ChainSettings GetChainSettingsFromDB()
        {

            string sql = "SELECT Data FROM Block AS b " +
                         "JOIN Transactions AS t ON b.Height = t.BlockID " +
                         "JOIN Data as d ON t.ID = d.TransID " +
                         "WHERE Height = 0 " +
                         "ORDER BY _ArrayIndex;";

            using (SQLiteDataReader reader = QuerySQL(sql))
            {

                if (!reader.Read())
                    return null;

                ChainSettings settings = new ChainSettings(reader);
                return settings;
            }
        }

        public Dictionary<string, ISCType> GetVariablesFromDB(long ID)
        {

            string sql = $"SELECT Name,Value FROM Variables WHERE SmartID={ID}";

            using (SQLiteDataReader reader = QuerySQL(sql)) {
                var list = new Dictionary<string, ISCType>();

                while (reader.Read())
                {
                    list.Add((string)reader[0], ((string)reader[1]).ConvertToInternalType());
                }
                return list;
            }

        }

        public int GetDifficulty(long? Height)
        {

            if (Height == null || Height == 0)
                return 7;

            //if (!ExistedBefore)
            //    throw new ArgumentException("Database is certainly empty!");

            long epochCount = ChainSettings.DifficultyAdjustment;
            int goal = ChainSettings.BlockTime;

            //height of last epoch before:
            long consolidationHeight = (long)Height / epochCount * epochCount;

            //if we go below 0 with height, we use genesis block as HeightA, but this means we need to reduce epochcount by 1
            int flag = 0;

            //both blocktimes ... A happened before B
            long HeightOfA = consolidationHeight - 1 - epochCount;
            if (HeightOfA < 0)
            {
                HeightOfA = 0;
                flag = 1;
            }

            long? timeA = GetBlock(HeightOfA)?.Time;
            long? timeB = GetBlock(consolidationHeight - 1)?.Time;

            //if B is not null, then we can compute the new difficulty
            if (timeB == null || timeA == null)
                return 7;

            //compute multiplier
            float multiplier = goal / (((long)timeB - (long)timeA) / (epochCount - flag));

            //get current difficulty
            int? currentDifficulty = GetBlock(consolidationHeight - 1)?.Difficulty;

            if (currentDifficulty == null)
                return 7;

            //calculate the difficulty change
            var precedingZerosChange = Cryptography.CalculateDifficultyChange(multiplier);

            //overloaded minus operator for difficulty
            return (int)currentDifficulty + precedingZerosChange;

        }

        public int GetDifficulty(Way way)
        {

            if (way == null)
                return 7;

            long epochCount = ChainSettings.DifficultyAdjustment;
            int goal = ChainSettings.BlockTime;


            //height of last epoch before:
            long consolidationHeight = way.CurrentBlock.Height / epochCount * epochCount;

            //if we go below 0 with height, we use genesis block as HeightA, but this means we need to reduce epochcount by 1
            int flag = 0;

            //both blocktimes ... A happened before B
            long HeightOfA = consolidationHeight - 1 - epochCount;
            if (HeightOfA < 0)
            {
                HeightOfA = 0;
                flag = 1;
            }

            //both blocktimes ... A happened before B g
            long? timeA = way.GetWayViaHeight(HeightOfA)?.CurrentBlock.Time ?? GetBlock(HeightOfA)?.Time;
            long? timeB = way.GetWayViaHeight(consolidationHeight - 1)?.CurrentBlock.Time ?? GetBlock(consolidationHeight - 1)?.Time;

            if (timeA == null || timeB == null)
                return 7;

            //compute multiplier
            float multiplier = goal / (((long)timeB - (long)timeA) / (epochCount - flag));

            //get current difficulty
            int? currentDifficulty = GetBlock(consolidationHeight - 1)?.Difficulty;

            if (currentDifficulty == null)
                return 7;

            //calculate the difficulty change
            var precedingZerosChange = Cryptography.CalculateDifficultyChange(multiplier);

            //overloaded minus operator for difficulty
            return (int)currentDifficulty + precedingZerosChange;

        }

        #endregion

        #region Balance Balance Balance Balance Balance Balance Balance Balance 

        public long GetBalance(string user)
        {

            //count all incoming money
            //get all block rewards & transfees
            var blockReward = GetBlockReward(user);

            //get all outputs who point towards you
            var OutputSum = GetIncomingOutputs(user);

            //count now all removing money
            //remove all outputs which are outgoing
            var OutgoingOutputs = GetOutgoingOutputs(user);

            //remove all transaction fees (transactions)
            var OutgoingTransfees = GetOutcomingTransFees(user);

            //remove all smartcontract fees
            var OutgoingSmartfees = GetOutcomingSmartFees(user);

            return blockReward + OutputSum + OutgoingOutputs + OutgoingTransfees + OutgoingSmartfees;
        }

        private long GetOutcomingSmartFees(string user)
        {
            string transFees = $"SELECT IFNULL(SUM(Fee),0) From Smartcontracts WHERE _FROM='{user}'";

            using (SQLiteDataReader reader = QuerySQL(transFees))
            {
                reader.Read();
                return (long)reader[0] * -1;
            }
        }

        private long GetOutcomingTransFees(string user)
        {
            string transFees = $"SELECT IFNULL(SUM(Data),0) FROM Transactions JOIN Data ON Transactions.ID = Data.TransID WHERE _From='{user}' AND _ArrayIndex = 0";

            using (SQLiteDataReader reader = QuerySQL(transFees))
            {
                reader.Read();
                return (long)reader[0] * -1;
            }
        }

        private long GetOutgoingOutputs(string user)
        {
            string sql_Outgoing = $"SELECT IFNULL(SUM(_Values),0) FROM Transactions JOIN Output ON Transactions.ID = Output.TransID WHERE _From='{user}' AND NOT Mode = -1;";

            using (SQLiteDataReader reader = QuerySQL(sql_Outgoing))
            {
                reader.Read();
                return (long)reader[0] * -1;
            }
        }

        private long GetIncomingOutputs(string user)
        {
            string sql_Outputs = $"SELECT IFNULL(SUM(_Values),0) FROM Output WHERE Receiver='{user}';";

            using (SQLiteDataReader reader = QuerySQL(sql_Outputs))
            {
                reader.Read();
                return (long)reader[0];
            }
        }

        private long GetBlockReward(string user)
        {

            string sql_blockRewards = $"SELECT IFNULL(COUNT(PublicKey),0) FROM Block WHERE PublicKey='{user}'";

            string sql_TransFees = "SELECT IFNULL(SUM(Data),0) FROM Block AS b JOIN Transactions AS t ON " +
                "b.Height = t.BlockID JOIN Data as d ON t.ID = d.TransID " +
                $"WHERE PublicKey = '{user}' AND _ArrayIndex = 0;";


            using (SQLiteDataReader reader = QuerySQL(sql_blockRewards), reader2 = QuerySQL(sql_TransFees))
            {

                reader.Read();
                reader2.Read();

                long blockReward = (long)reader[0] * ChainSettings.BlockReward;

                long TransFees = (long)reader2[0];

                return blockReward + TransFees;
            }
        }

        #endregion

        #region SQL Utils

        public SQLiteDataReader QuerySQL(string sql)
        {


            Db = new SQLiteConnection($@"Data Source={IXISettings.DataBasePath}{CoinName}\chain.db; Version=3;");
            Db.Open();

            SQLiteCommand command = new SQLiteCommand(Db);
            command.CommandText = sql;

            SQLiteDataReader reader = command.ExecuteReader(System.Data.CommandBehavior.CloseConnection);

            return reader;
        }

        public int NoQuerySQL(string sql)
        {

            Db = new SQLiteConnection($@"Data Source={IXISettings.DataBasePath}{CoinName}\chain.db; Version=3;");
            Db.Open();
            int num = 0;

            using (SQLiteCommand command = new SQLiteCommand(Db))
            {
                command.CommandText = sql;
                num = command.ExecuteNonQuery();
            }

            Db.Close();

            return num;
        }

        private string IsNull(long? num)
        {

            if (num == null)
                return "NULL";
            return num.ToString();

        }

        #endregion


    }


}