
require( "./env" );
import express from "express";
import path from "path";
import bodyParser from "body-parser";
import cookieParser from "cookie-parser";
import { v4 as uuidv4 } from "uuid";

async function startBackend() {
	const RefreshServerTimeout = 5*60*1000;
	const app = express();
	var isPassed = true;
	var getClientIp = (req) => (req.headers["x-forwarded-for"] || req.socket.remoteAddress);
	var servers = [];
	//
	app.use(bodyParser.json());
	app.use(bodyParser.urlencoded({ extended: false }));
	app.use(cookieParser(process.env.COOKIE_SECRET));
	//
//	await blog(app);
	// testGet
	app.get("/testGet", async (req, res) => {
		var dst = { txt: "eiei gum", };
		res.send(dst);
	});
	// testPost
	app.post("/testPost", async (req, res) => {
		var src = req.body;
		var dst = { txt: "post na", data: src };
		res.send(dst);
	});
	//
	var retErr = (logName, errMsg, dst, res) => {
		console.log(logName + " = " + errMsg);
		dst.errMsg = errMsg;
		res.send(dst);
		return;
	};
	//
	var retOk = (logName, dst, res) => {
		console.log(logName + " = ok");
		res.send(dst);
		return;
	}
	//
	var logErr = (logName, errMsg, dst) => { console.log(logName + " = " + errMsg); dst.errMsg = errMsg; return dst; };
	var logOk = (logName, dst) => { console.log(logName + " = ok"); return dst; }
	//
	app.post("/createServer", async (req, res) => {
		const fn = async () => {
			var src = req.body;
			var dst = { errMsg: null };
			var server = {};
			if(src.serverPass != process.env.SERVER_PASSWORD) { return logErr("/createServer", "serverPass failed", dst); }
			if(!src.serverGroup) { return logErr("/createServer", "serverGroup not assign", dst); }
			server.id = uuidv4();
			server.ip = getClientIp(req);
			server.port = src.port;
			server.group = src.serverGroup;
			server.clientNum = 0;
			server.maxClient = src.maxClient;
			server.timeoutId = setTimeout(() => { delete servers[servers.findIndex((s) => s.id == server.id)]; }, RefreshServerTimeout);
			servers.push(server);
			dst.id = server.id;
			return logOk("/createServer", dst);
		};
		//
		try { res.send(await fn()); }
		catch(err) { console.log(err); res.send( {errMsg: err.message} ); };
	});
	//
	app.post("/delServer", async (req, res) => {
		const fn = async () => {
			var src = req.body;
			var dst = { errMsg: null };
			var offset = servers.findIndex((s) => s.id == src.id);
			if(offset < 0) { return logErr("/delServer", "Can not find sever", dst); }
			clearTimeout(servers[offset].timeoutId);
			delete servers[offset];
			return logOk("/delServer", dst);
		};
		//
		try { res.send(await fn()); }
		catch(err) { console.log(err); res.send( {errMsg: err.message} ); };
	});
	//
	app.post("/refreshServer", async (req, res) => {
		const fn = async () => {
			var src = req.body;
			var dst = { errMsg: null };
			var offset = servers.findIndex((s) => s.id == src.id);
			if(offset < 0) { return logErr("/refreshServer", "Can not find sever", dst); }
			var server = servers[offset];
			server.clientNum = src.clientNum;
			server.maxClient = src.maxClient;
			clearTimeout(server.timeoutId);
			server.timeoutId = setTimeout(() => { delete servers[servers.findIndex((s) => s.id == server.id)]; }, RefreshServerTimeout );
			return logOk("/refreshServer", dst);
		};
		//
		try { res.send(await fn()); }
		catch(err) { console.log(err); res.send( {errMsg: err.message} ); };
	});
	//
	app.post("/getServer", async (req, res) => {
		const fn = async () => {
			var src = req.body;
			var dst = { errMsg: null, servers: [], };
			servers.forEach((server, offset) => {
				if(src.serverGroup === server.group) { dst.servers.push({
					ip: server.ip,
					port: server.port,
					clientNum: server.clientNum,
					maxClient: server.maxClient,
				}) }
			});
			return retOk("/getServer", dst, res);
		};
		//
		try { res.send(await fn()); }
		catch(err) { console.log(err); res.send( {errMsg: err.message} ); };
	});
	//
	console.log("NODE_ENV =", process.env.NODE_ENV);
	console.log("TEST_ENV =", process.env.TEST_ENV);
	app.set("port", (process.env.PORT || 3001));
	app.listen(app.get("port"), "0.0.0.0", () => { console.log(`Listening on ${app.get('port')}`); });
}

startBackend();

