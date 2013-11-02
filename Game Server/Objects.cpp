#include "Objects.h"

using namespace game_server;
using namespace game_server::objects;

updatable::updatable() {
	
}

updatable::~updatable() {

}

base_obj::base_obj(object_type obj_type) {
	this->owner = 0;
	this->obj_type = obj_type;
	this->last_updated_by_cache = date_time::clock::now();
}

base_obj::~base_obj() {

}

map_obj::map_obj(object_type obj_type) : base_obj(obj_type) {

}

map_obj::~map_obj() {

}