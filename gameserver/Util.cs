
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Proyecto26;
using Proto.Promises;

public class Util {
	//
	public static Promise<string> httpPost(string url, object body) {
		return Promise.New<string>(deferred => {
			RestClient.Post(url, body).Then(res => {
				deferred.Resolve(res.Text);
			}).Catch((err) => {
//				deferred.Reject(err);
				deferred.Resolve($"{{\"errMsg\":\"{err.Message}\"}}");
			});
		});
	}
	//
}

public class UtUid {
	int next = 1;
	List<int> lKeep = new List<int>();

	public int gen() {
		if(lKeep.Count > 0) {
			int id = lKeep[lKeep.Count - 1];
			lKeep.RemoveAt(lKeep.Count - 1);
			return id;
		}
		return next++;
	}

	public void remove(int id) { lKeep.Add(id); }

	public void clear() {
		next = 1;
		lKeep = new List<int>();
	}
}

