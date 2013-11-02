#include "Objects.h"

using namespace game_server;
using namespace game_server::objects;

updatable::updatable() {
	
}

updatable::~updatable() {

}

base_obj::base_obj(obj_type object_type) {
	this->owner = 0;
	this->object_type = object_type;
	this->last_updated_by_cache = date_time::clock::now();
}

base_obj::~base_obj() {

}

map_obj::map_obj(obj_type object_type) : base_obj(object_type) {

}

map_obj::~map_obj() {

}