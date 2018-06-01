﻿using System;
using System.Collections.Generic;
using NUnit.Framework;
using TangleChain;
using TangleChain.Classes;

namespace TangleChainTest.UnitTests {

    [TestFixture]
    public class TestDatabase {

        [Test]
        public void TestInitialize() {

            DataBase db = new DataBase("Test");

            Assert.IsNotNull(db);
            Assert.IsTrue(db.IsWorking());

        }

        [Test]
        public void BlockAddGet() {

            Block test = new Block();
            DataBase db = new DataBase("Test");

            db.AddBlock(test,false);

            Block compare = db.GetBlock(test.Height);

            Assert.AreEqual(test, compare);

        }

        [Test]
        public void DownloadChainAndStorage() {
            
            Settings.Default(true);
            
            string address = "KLOSOCIGXDGVBEPRPPTOKH9TAVAGSMUEFXFXXLQJGASXVCJPAUXDIVGKNRSIOVWPUDNBKEPRPMCGNEYKT";
            string hash = "J9GZJJN9BEGJBYOVUZ9VEOHBAXAGBUTPLBIPDZRGARH9BYCWHWHHSHK9ILPKNXBGAZIRY9OKFCCQMZ9FL";
            int difficulty = 5;

            string expectedHash = "VIIKHLWDHCJAWGXXPGDGPCBJQRRSHFS9LERIAUKPSZBZTQJIVJYF9QJCJALPBYSKYNRXGETEPEBVWNRVM";

            Block latest = Core.DownloadChain(address, hash, difficulty, true);

            Assert.AreEqual(latest.Hash, expectedHash);

            DataBase db = new DataBase(latest.CoinName);

            Block storedBlock = db.GetBlock(latest.Height);

            Assert.AreEqual(latest, storedBlock);

        }

        [Test]
        public void GetBalance() {

            DataBase db = new DataBase("SGMRRHWVZS");

            int balance = db.GetBalance("ME");

            Console.WriteLine("Balance: " + balance);

            //int transFees = db.GetAllReceivings("ME");

            //Assert.AreEqual(20*30, transFees);

        }

        [Test]
        public void TransactionUpDownStorage() {

            string sendTo = Utils.GenerateRandomString(81);
            Settings.Default(true);

            Transaction uploadTrans = new Transaction("ME", 0, sendTo);
            uploadTrans.AddFee(30);
            uploadTrans.AddOutput(100, "YOU");
            uploadTrans.Final();

            Core.UploadTransaction(uploadTrans);

            var transList = Core.GetAllTransactionsFromAddress(sendTo);

            Transaction trans = transList[0];

            DataBase db = new DataBase(Utils.GenerateRandomString(10));

            db.AddTransactionToDatabase(transList);

            Transaction compare = db.GetTransaction(trans.SendTo, trans.Hash);

            Assert.AreEqual(trans, compare);

        }



        

    }
}
